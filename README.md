# azure-function-cosmos-example

Azure Function App (.NET 10, isolated worker) with Cosmos DB CRUD operations for a **Consent Service** example.

## Overview

This project is a minimal, self-contained example that demonstrates how to build an HTTP-triggered
Azure Function App on the **.NET 10 isolated worker model**, using **Azure Cosmos DB (SQL API)** as
the data store for consent records.

## Features

- ✅ Azure Functions v4, isolated worker, targeting **.NET 10**
- ✅ HTTP triggers for full CRUD operations
- ✅ Azure Cosmos DB SDK (`Microsoft.Azure.Cosmos`) with dependency injection
- ✅ Consistent error handling and structured logging
- ✅ Simple continuation-token based pagination for listing records

## Project Structure

```
azure-function-cosmos-example/
├── src/
│   ├── Models/
│   │   └── Consent.cs                 # Consent document model
│   ├── Services/
│   │   └── CosmosDbService.cs         # ICosmosDbService + Cosmos DB implementation
│   ├── Functions/
│   │   ├── CreateConsentFunction.cs   # POST /api/consents
│   │   ├── GetConsentFunction.cs      # GET /api/consents/{id}
│   │   ├── ListConsentsFunction.cs    # GET /api/consents
│   │   ├── UpdateConsentFunction.cs   # PUT /api/consents/{id}
│   │   └── DeleteConsentFunction.cs   # DELETE /api/consents/{id}
│   └── Program.cs                     # Entry point / startup: DI + CosmosClient registration
├── ConsentFunctionsApp.csproj
├── host.json
├── local.settings.json.example
├── README.md
└── .gitignore
```

## Consent Model

```csharp
public class Consent
{
    public string Id { get; set; }
    public string UserId { get; set; }       // Cosmos DB partition key
    public string ConsentType { get; set; }
    public bool IsAgreed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

The Cosmos DB container should use `/userId` as its **partition key path**.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (preview)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- An Azure Cosmos DB account (SQL API), or the [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator) for local development
- A local storage emulator such as [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for the Functions host

## Setup

1. **Clone the repository** and restore dependencies:

   ```bash
   dotnet restore
   ```

2. **Create the Cosmos DB database and container** (name `ConsentServiceDb` / `Consents` by default,
   partition key `/userId`), either in the Azure Portal or via the Azure CLI:

   ```bash
   az cosmosdb sql database create \
     --account-name <your-cosmos-account> \
     --resource-group <your-resource-group> \
     --name ConsentServiceDb

   az cosmosdb sql container create \
     --account-name <your-cosmos-account> \
     --resource-group <your-resource-group> \
     --database-name ConsentServiceDb \
     --name Consents \
     --partition-key-path /userId
   ```

3. **Configure local settings**: copy `local.settings.json.example` to `local.settings.json` and
   fill in your Cosmos DB connection string:

   ```bash
   cp local.settings.json.example local.settings.json
   ```

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "CosmosDb:ConnectionString": "AccountEndpoint=https://<your-cosmos-account>.documents.azure.com:443/;AccountKey=<your-account-key>;",
       "CosmosDb:DatabaseName": "ConsentServiceDb",
       "CosmosDb:ContainerName": "Consents"
     }
   }
   ```

   > `local.settings.json` is ignored by git (see `.gitignore`) so your secrets are never committed.

4. **Run the function app locally**:

   ```bash
   func start
   ```

## API Reference

All endpoints are served under the `/api` route prefix (configured in `host.json`).

### Create a consent

```
POST /api/consents
Content-Type: application/json

{
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": true
}
```

**Response** `201 Created`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": true,
  "createdAt": "2025-01-01T12:00:00Z",
  "updatedAt": "2025-01-01T12:00:00Z"
}
```

### Get a consent by id

```
GET /api/consents/{id}?userId=user-123
```

> The `userId` query parameter is optional. Supplying it avoids a cross-partition query
> since `userId` is the Cosmos DB partition key.

**Response** `200 OK` with the consent document, or `404 Not Found`.

### List consents (paginated)

```
GET /api/consents?pageSize=20&continuationToken=<token-from-previous-response>
```

**Response** `200 OK`

```json
{
  "items": [ { "id": "...", "userId": "user-123", "consentType": "marketing", "isAgreed": true } ],
  "continuationToken": "opaque-token-or-null"
}
```

### Update a consent

```
PUT /api/consents/{id}?userId=user-123
Content-Type: application/json

{
  "consentType": "marketing",
  "isAgreed": false
}
```

**Response** `200 OK` with the updated document, or `404 Not Found`.

### Delete a consent

```
DELETE /api/consents/{id}?userId=user-123
```

**Response** `204 No Content`, or `404 Not Found`.

## Error Handling

All functions return structured JSON error responses, e.g.:

```json
{ "error": "UserId and ConsentType are required." }
```

Errors are logged via the standard `ILogger<T>` abstraction, and Application Insights integration
is available out of the box through `Microsoft.Azure.Functions.Worker.ApplicationInsights` (set
the `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting to enable it).

## Notes

- This is an **example/sample** project intended for learning purposes; add authentication,
  request validation, and production-grade hardening before using it in production.
- `AuthorizationLevel.Function` is used for the HTTP triggers. Provide the function key
  (`x-functions-key` header or `?code=` query string) when calling the endpoints, or change the
  authorization level to `Anonymous` for local testing.
