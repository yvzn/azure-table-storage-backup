# azure-table-storage-backup

Automated Azure Table Storage Backup: run periodic backups of Azure Storage Tables to another Storage Account.

## Run locally

### Tooling Required

To run this project locally, you will need the following:

- [.NET SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [NodeJS](https://nodejs.org/en/download/package-manager/current) (for local Azure Storage emulation)

### Running a Test Database

Azurite runs a local Azure Storage emulator. By creating 2 accounts in Azurite (*devstoreaccount1* and *devstoreaccount2*), it can be used for both the backup source and the backup destination.

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

| Setting | Value |
|---|---|
| BACKUP_SOURCE_CONNECTION_STRING | connection string to the backup source account |
| BACKUP_DESTINATION_CONNECTION_STRING | connection string to the backup destination account |
| BACKUP_DAILY_SCHEDULE | [Cron expression](https://github.com/atifaziz/NCrontab) to define the periodicity of the backup |


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

## Licensing

This open source software is distributed under MIT license, please refer to [LICENSE](LICENSE) file.

(c) 2024 Yvan Razafindramanana

### Third party licenses

This project uses open-source, third party software:

- [.NET SDK](https://github.com/dotnet/sdk): MIT License, Copyright (c) .NET Foundation
- [Azure Function Core Tools](https://github.com/Azure/azure-functions-core-tools): MIT License, Copyright (c) .NET Foundation
- [Azurite](https://github.com/Azure/Azurite): MIT License, Copyright (c) Microsoft Corporation
