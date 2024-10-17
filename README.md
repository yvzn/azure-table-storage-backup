# azure-table-storage-backup

Automated Azure Table Storage Backup: run periodic backups of Azure Storage Tables to another Storage Account.

## Deploy in Azure

To deploy the function app to Azure, follow these steps:

1. Fork the repository to your GitHub account.
2. Create a Azure Function App in the Azure Portal (Use .NET 8 isolated runtime).
3. Create a pipeline in Azure DevOps using the provided `azure-pipelines.yml` file.
4. Add the necessary variables and environments required by the `azure-pipelines.yml` file to your Azure DevOps pipeline configuration:

Variables:

- `functionAppName`: The name of the Azure Function App you created in the Azure Portal.
- `azureSubscription`: The Azure subscription ID where the Function App will be deployed.

Environment:

- `Azure`: This environment should be configured in Azure DevOps to include the necessary service connections and permissions to deploy resources to your Azure subscription.

5. Add [required configurations / environment variables](#configuration) in Azure Function App

## Run locally

### Tooling Required

To run this project locally, you will need the following:

- [.NET SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [NodeJS](https://nodejs.org/en/download/package-manager/current) (for local Azure Storage emulation)

### Running a Test Database

Azurite runs a local Azure Storage emulator. By [creating 2 accounts in Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=npm%2Cblob-storage#custom-storage-accounts-and-keys) (*devstoreaccount1* and *devstoreaccount2*), it can be used for both the backup source and the backup destination.

Navigate to the `db/` directory and run the following command based on your operating system:

#### Windows

```sh
npm install
npm run dev:windows
```

#### Linux

```sh
npm install
npm run dev:linux
```
This will start the Azurite emulator with the necessary configuration for local development.

### Configuration in local.settings.json

Copy the `local.settings.json.sample` file to `local.settings.json` in the `src/` directory and update the configuration as needed:

| <a name="configuration" id="configuration"></a> Setting | Value |
|---|---|
| BACKUP_SOURCE_CONNECTION_STRING | connection string to the backup source account |
| BACKUP_DESTINATION_CONNECTION_STRING | connection string to the backup destination account |
| BACKUP_SOURCE_TABLES | comma separated list of tables to backup from source account |
| BACKUP_DAILY_SCHEDULE | [Cron expression](https://github.com/atifaziz/NCrontab) to define the periodicity of the backup |
| BACKUP_WEEKLY_SCHEDULE | [Cron expression](https://github.com/atifaziz/NCrontab) to define the periodicity of the weekly backup |
| BACKUP_MONTHLY_SCHEDULE | [Cron expression](https://github.com/atifaziz/NCrontab) to define the periodicity of the monthly backup |

The project will backup the data of `BACKUP_SOURCE_TABLES`, from the Azure Storage account described by `BACKUP_SOURCE_CONNECTION_STRING` to the Azure Storage account described by `BACKUP_DESTINATION_CONNECTION_STRING`.

### Building and Running the Azure Function

Navigate to the `src/` directory and run the following commands to build and start the Azure Function:

```sh
dotnet build
func start
```

This will build the project and start the Azure Functions runtime.

### Invoking the Test Endpoint
Once the Azure Function is running, you can invoke the test endpoint using a tool like `curl` or Insomnia. Assuming the function is running in Debug on the default port (7071), you can use the following command:

```sh
curl -X POST http://localhost:7071/api/BackupTables
```

This will trigger the *BackupTablesTest* function and you should see the logs in the console indicating the function execution.

## Restoration Process

Restoration can be achieved by simply swapping the source and backup storage accounts in the client applications. This is the advantage of having a storage account as a backup medium.

By configuring the client applications to use the backup storage account as the source, you can easily restore the data to its original state or to a new storage account.

## Licensing

This open source software is distributed under MIT license, please refer to [LICENSE](LICENSE) file.

(c) 2024 Yvan Razafindramanana

### Third party licenses

This project uses open-source, third party software:

- [.NET SDK](https://github.com/dotnet/sdk): MIT License, Copyright (c) .NET Foundation
- [Azure Function Core Tools](https://github.com/Azure/azure-functions-core-tools): MIT License, Copyright (c) .NET Foundation
- [Azurite](https://github.com/Azure/Azurite): MIT License, Copyright (c) Microsoft Corporation
