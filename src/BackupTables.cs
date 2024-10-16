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

	[Function("BackupTablesDaily")]
	public async Task RunAsync(
		[TimerTrigger("%BACKUP_DAILY_SCHEDULE%")]
		TimerInfo timerInfo)
	{
		await BackupTablesAsync();
	}

#if DEBUG
	[Function("BackupTablesTest")]
	public async Task<IActionResult> TestAsync(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get")]
		HttpRequest req)
	{
		await BackupTablesAsync();
		return new OkResult();
	}
#endif

	private Task BackupTablesAsync()
		=> Task.WhenAll(tablesToBackup.Select(BackupTableAsync));

	private async Task BackupTableAsync(string tableName)
	{
		try
		{
			log.LogInformation("Backup of table {TableName}", tableName);

			var resiliencePipeline = GetResiliencePipeline(log);

			await resiliencePipeline.ExecuteAsync(
				async (CancellationToken ct) => await destinationTableServiceClient.DeleteTableIfExistsAsync(tableName, ct));

			await resiliencePipeline.ExecuteAsync(
				async (CancellationToken ct) => await destinationTableServiceClient.CreateTableAsync(tableName, ct));

			var entityCount = await BackupEntities(tableName);

			log.LogInformation("Backup of table {TableName} completed with {EntityCount} entries", tableName, entityCount);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Backup of table {TableName} failed", tableName);
		}
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
