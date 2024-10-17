using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Polly;
using Polly.Retry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace backup;

public class BackupTables(ILogger<BackupTables> log)
{
	private static readonly TableServiceClient destinationTableServiceClient = new(Environment.GetEnvironmentVariable("BACKUP_DESTINATION_CONNECTION_STRING"));

	private static readonly string[] tablesToBackup = ExtractTableNames(Environment.GetEnvironmentVariable("BACKUP_SOURCE_TABLES"));

	private enum BackupFrequency { Daily, Weekly, Monthly };

	[Function("BackupTablesDaily")]
	public async Task RunDailyAsync(
		[TimerTrigger("%BACKUP_DAILY_SCHEDULE%")]
		TimerInfo timerInfo)
	{
		await BackupTablesAsync(BackupFrequency.Daily);
	}

	[Function("BackupTablesWeekly")]
	public async Task RunWeeklyAsync(
		[TimerTrigger("%BACKUP_WEEKLY_SCHEDULE%")]
		TimerInfo timerInfo)
	{
		await BackupTablesAsync(BackupFrequency.Weekly);
	}

	[Function("BackupTablesMonthly")]
	public async Task RunMonthlyAsync(
		[TimerTrigger("%BACKUP_MONTHLY_SCHEDULE%")]
		TimerInfo timerInfo)
	{
		await BackupTablesAsync(BackupFrequency.Monthly);
	}

#if DEBUG
	[Function("BackupTablesTest")]
	public async Task<IActionResult> TestAsync(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get")]
		HttpRequest req)
	{
		await BackupTablesAsync(BackupFrequency.Daily);
		return new OkResult();
	}
#endif

	private Task BackupTablesAsync(BackupFrequency frequency)
		=> Task.WhenAll(tablesToBackup.Select(tableName => BackupTableAsync(tableName, frequency)));

	private async Task BackupTableAsync(string tableName, BackupFrequency frequency)
	{
		try
		{
			log.LogInformation("Backup of table {TableName} with frequency {Frequency} started", tableName, frequency);

			var resiliencePipeline = GetResiliencePipeline(log);

			string backupTableName = GenerateBackupTableName(tableName, frequency);

			await resiliencePipeline.ExecuteAsync(
				async (CancellationToken ct) => await destinationTableServiceClient.DeleteTableIfExistsAsync(backupTableName, ct));

			await resiliencePipeline.ExecuteAsync(
				async (CancellationToken ct) => await destinationTableServiceClient.CreateTableAsync(backupTableName, ct));

			var entityCount = await BackupEntities(tableName);

			log.LogInformation("Backup of table {TableName} with frequency {Frequency} completed with {EntityCount} entries", tableName, frequency, entityCount);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Backup of table {TableName} with frequency {Frequency} failed", tableName, frequency);
		}
	}

	private static string GenerateBackupTableName(string tableName, BackupFrequency frequency)
	{
		return frequency == BackupFrequency.Daily ? tableName : $"{tableName}_{frequency.ToString().ToLowerInvariant()}";
	}

	private static async Task<int> BackupEntities(string tableName)
	{
		var entityCount = 0;
		var sourceTableClient = new TableClient(Environment.GetEnvironmentVariable("BACKUP_SOURCE_CONNECTION_STRING"), tableName);
		var destinationTableClient = new TableClient(Environment.GetEnvironmentVariable("BACKUP_DESTINATION_CONNECTION_STRING"), tableName);

		await foreach (var sourceEntity in sourceTableClient.QueryAsync<TableEntity>(_ => true))
		{
			var backupEntity = new TableEntity
			{
				PartitionKey = sourceEntity.PartitionKey,
				RowKey = sourceEntity.RowKey
			};
			foreach (var property in sourceEntity.Keys)
			{
				backupEntity[property] = sourceEntity[property];
			}
			await destinationTableClient.AddEntityAsync(backupEntity);
			++entityCount;
		}

		return entityCount;
	}

	private static string[] ExtractTableNames(string? commaSeparatedTableNames) =>
		commaSeparatedTableNames?
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToArray() ?? [];

	private static ResiliencePipeline GetResiliencePipeline(ILogger logger)
		=> new ResiliencePipelineBuilder()
			.AddRetry(new()
			{
				ShouldHandle = new PredicateBuilder().Handle<Azure.RequestFailedException>(),
				Delay = TimeSpan.FromSeconds(5),
				MaxRetryAttempts = 10,
				BackoffType = DelayBackoffType.Exponential,
				OnRetry = (OnRetryArguments<object> args) =>
				{
					logger.LogDebug("Retry attempt: {AttemptNumber} delay: {RetryDelay}", args.AttemptNumber, args.RetryDelay);
					return ValueTask.CompletedTask;
				}
			})
			.AddTimeout(TimeSpan.FromMinutes(2))
			.Build();
}

public static class TableServiceClientExtensions
{
	public async static Task DeleteTableIfExistsAsync(this TableServiceClient tableServiceClient, string tableName, CancellationToken cancellationToken)
	{
		var queryTableResults = tableServiceClient.QueryAsync(filter: $"TableName eq '{tableName}'", cancellationToken: cancellationToken);
		await foreach (var table in queryTableResults.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			await tableServiceClient.DeleteTableAsync(table.Name, cancellationToken);
		}
	}
}
