# Calling the Azure Function App from C#

This guide shows how to call the HTTP-triggered consent endpoints from C# clients using an API key.
The examples match the functions implemented in this repository and use the `x-functions-key` header,
which is the recommended option for service-to-service callers.

## Prerequisites and setup

- .NET 10 SDK
- A running Azure Functions app from this repository
- A function key from Azure Portal or the Functions host
- The base URL for your deployed or local function app

### Get the function key

Use either of these approaches:

1. **Azure Portal**
   - Open your Function App
   - Go to **Functions** or **App keys**
   - Copy a function key or host key
2. **Request authentication**
   - Prefer the `x-functions-key` header for service callers
   - Azure Functions also supports `?code=<key>` in the query string, but headers keep URLs cleaner and safer for logs

## Authentication (API key)

All consent endpoints use `AuthorizationLevel.Function`, so callers must send a function key.

```http
x-functions-key: YOUR_FUNCTION_KEY
```

## Base URL and endpoint structure

Use the `/api` route prefix configured by `host.json`.

- Local base URL: `http://localhost:7071/api/`
- Azure base URL: `https://<your-function-app>.azurewebsites.net/api/`

| Operation | Method | Route | Notes |
| --- | --- | --- | --- |
| Create Consent | `POST` | `/consents` | Creates a new consent record |
| Get Consent by ID | `GET` | `/consents/{id}` | Optional `userId` query improves Cosmos partition lookup |
| List All Consents | `GET` | `/consents` | Supports `pageSize` and `continuationToken` |
| Update Consent | `PUT` | `/consents/{id}` | Optional `userId` query improves Cosmos partition lookup |
| Delete Consent | `DELETE` | `/consents/{id}` | Optional `userId` query improves Cosmos partition lookup |

## Full API reference

### 1. Create Consent

**Request**

```http
POST /api/consents
Content-Type: application/json
x-functions-key: YOUR_FUNCTION_KEY

{
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": true
}
```

**curl**

```bash
curl -X POST "https://your-function-app.azurewebsites.net/api/consents" \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user-123",
    "consentType": "marketing",
    "isAgreed": true
  }'
```

**Response** `201 Created`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": true,
  "createdAt": "2026-09-10T07:00:00Z",
  "updatedAt": "2026-09-10T07:00:00Z"
}
```

### 2. Get Consent by ID

**Request**

```http
GET /api/consents/{id}?userId=user-123
x-functions-key: YOUR_FUNCTION_KEY
```

**curl**

```bash
curl "https://your-function-app.azurewebsites.net/api/consents/3fa85f64-5717-4562-b3fc-2c963f66afa6?userId=user-123" \
  -H "x-functions-key: YOUR_FUNCTION_KEY"
```

**Response** `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": true,
  "createdAt": "2026-09-10T07:00:00Z",
  "updatedAt": "2026-09-10T07:00:00Z"
}
```

### 3. List All Consents

**Request**

```http
GET /api/consents?pageSize=20&continuationToken=<opaque-token>
x-functions-key: YOUR_FUNCTION_KEY
```

**curl**

```bash
curl "https://your-function-app.azurewebsites.net/api/consents?pageSize=20" \
  -H "x-functions-key: YOUR_FUNCTION_KEY"
```

**Response** `200 OK`

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "user-123",
      "consentType": "marketing",
      "isAgreed": true,
      "createdAt": "2026-09-10T07:00:00Z",
      "updatedAt": "2026-09-10T07:00:00Z"
    }
  ],
  "continuationToken": null
}
```

### 4. Update Consent

**Request**

```http
PUT /api/consents/{id}?userId=user-123
Content-Type: application/json
x-functions-key: YOUR_FUNCTION_KEY

{
  "consentType": "marketing",
  "isAgreed": false
}
```

**curl**

```bash
curl -X PUT "https://your-function-app.azurewebsites.net/api/consents/3fa85f64-5717-4562-b3fc-2c963f66afa6?userId=user-123" \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "consentType": "marketing",
    "isAgreed": false
  }'
```

**Response** `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-123",
  "consentType": "marketing",
  "isAgreed": false,
  "createdAt": "2026-09-10T07:00:00Z",
  "updatedAt": "2026-09-10T08:00:00Z"
}
```

### 5. Delete Consent

**Request**

```http
DELETE /api/consents/{id}?userId=user-123
x-functions-key: YOUR_FUNCTION_KEY
```

**curl**

```bash
curl -X DELETE "https://your-function-app.azurewebsites.net/api/consents/3fa85f64-5717-4562-b3fc-2c963f66afa6?userId=user-123" \
  -H "x-functions-key: YOUR_FUNCTION_KEY"
```

**Response** `204 No Content`

## C# client examples with HttpClient

The repository includes a sample project at `/samples/ConsentFunctionsClient.Console` that uses dependency injection and `HttpClientFactory`.

### Configuration example (`appsettings.json`)

```json
{
  "FunctionApp": {
    "BaseUrl": "https://your-function-app.azurewebsites.net/api/",
    "ApiKey": "YOUR_FUNCTION_KEY"
  }
}
```

### Shared models used by the examples

```csharp
public sealed record CreateConsentRequest(string UserId, string ConsentType, bool IsAgreed);
public sealed record UpdateConsentRequest(string ConsentType, bool IsAgreed);
```

### Create Consent (`POST`)

```csharp
using var client = new HttpClient
{
    BaseAddress = new Uri("https://your-function-app.azurewebsites.net/api/")
};

using var request = new HttpRequestMessage(HttpMethod.Post, "consents")
{
    Content = JsonContent.Create(new CreateConsentRequest("user-123", "marketing", true))
};
request.Headers.Add("x-functions-key", configuration["FunctionApp:ApiKey"]!);

using HttpResponseMessage response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
Consent? created = await response.Content.ReadFromJsonAsync<Consent>();
```

### Get Consent by ID (`GET` single)

```csharp
using var request = new HttpRequestMessage(
    HttpMethod.Get,
    $"consents/{consentId}?userId={Uri.EscapeDataString(userId)}");
request.Headers.Add("x-functions-key", configuration["FunctionApp:ApiKey"]!);

using HttpResponseMessage response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
Consent? consent = await response.Content.ReadFromJsonAsync<Consent>();
```

### List All Consents (`GET` all)

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, "consents?pageSize=20");
request.Headers.Add("x-functions-key", configuration["FunctionApp:ApiKey"]!);

using HttpResponseMessage response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
PagedConsentsResponse? page = await response.Content.ReadFromJsonAsync<PagedConsentsResponse>();
```

### Update Consent (`PUT`)

```csharp
using var request = new HttpRequestMessage(
    HttpMethod.Put,
    $"consents/{consentId}?userId={Uri.EscapeDataString(userId)}")
{
    Content = JsonContent.Create(new UpdateConsentRequest("marketing", false))
};
request.Headers.Add("x-functions-key", configuration["FunctionApp:ApiKey"]!);

using HttpResponseMessage response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
Consent? updated = await response.Content.ReadFromJsonAsync<Consent>();
```

### Delete Consent (`DELETE`)

```csharp
using var request = new HttpRequestMessage(
    HttpMethod.Delete,
    $"consents/{consentId}?userId={Uri.EscapeDataString(userId)}");
request.Headers.Add("x-functions-key", configuration["FunctionApp:ApiKey"]!);

using HttpResponseMessage response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();
```

## Async/await patterns

Two common patterns are shown in the repository:

1. **Top-level application flow** — `Program.cs` uses `await` from an `async Main` style entrypoint.
2. **Reusable client methods** — `ConsentFunctionsClient` exposes `Task<T>` methods such as `CreateConsentAsync` and `ListConsentsAsync` for DI-friendly service usage.

## Using HttpClientFactory with dependency injection

This is the same pattern used by the sample console app:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<FunctionAppClientOptions>()
    .Bind(builder.Configuration.GetSection("FunctionApp"));

builder.Services.AddHttpClient<IConsentFunctionsClient, ConsentFunctionsClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FunctionAppClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
```

## Error handling example

Use a custom exception or inspect the status code before deserializing.

```csharp
using HttpResponseMessage response = await client.SendAsync(request);
string responseBody = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    throw new InvalidOperationException(
        $"Function call failed with {(int)response.StatusCode}: {responseBody}");
}
```

Typical error payloads from this function app:

```json
{ "error": "UserId and ConsentType are required." }
```

```json
{ "error": "Consent '123' was not found." }
```

## RestSharp example

If you prefer RestSharp in another client project, the same API key header can be added like this:

```csharp
var restClient = new RestClient("https://your-function-app.azurewebsites.net/api/");
var request = new RestRequest("consents", Method.Post);
request.AddHeader("x-functions-key", apiKey);
request.AddJsonBody(new CreateConsentRequest("user-123", "marketing", true));

RestResponse<Consent> response = await restClient.ExecuteAsync<Consent>(request);
if (!response.IsSuccessful || response.Data is null)
{
    throw new InvalidOperationException(response.Content ?? "Function call failed.");
}
```

## Unit test examples for callers

The repository does not include a live integration test host, but you can unit test your client code by faking `HttpMessageHandler`.

```csharp
public sealed class FakeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("x-functions-key", request.Headers.Single().Key);

        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""
            {
              "id": "abc",
              "userId": "user-123",
              "consentType": "marketing",
              "isAgreed": true,
              "createdAt": "2026-09-10T07:00:00Z",
              "updatedAt": "2026-09-10T07:00:00Z"
            }
            """)
        };

        return Task.FromResult(response);
    }
}
```

```csharp
[Fact]
public async Task CreateConsentAsync_Adds_Function_Key_Header()
{
    var httpClient = new HttpClient(new FakeHandler())
    {
        BaseAddress = new Uri("https://example.azurewebsites.net/api/")
    };

    var options = Options.Create(new FunctionAppClientOptions
    {
        BaseUrl = "https://example.azurewebsites.net/api/",
        ApiKey = "test-key"
    });

    var client = new ConsentFunctionsClient(httpClient, options);
    Consent created = await client.CreateConsentAsync(new CreateConsentRequest("user-123", "marketing", true));

    Assert.Equal("abc", created.Id);
}
```

## Error codes and handling

| Status code | Meaning | Typical cause |
| --- | --- | --- |
| `200 OK` | Read or update succeeded | Valid request |
| `201 Created` | Create succeeded | Valid create request |
| `204 No Content` | Delete succeeded | Existing consent deleted |
| `400 Bad Request` | Validation or JSON issue | Missing fields or malformed JSON |
| `404 Not Found` | Consent does not exist | Unknown `id` or wrong `userId` |
| `500 Internal Server Error` | Server-side failure | Cosmos DB or unexpected runtime issue |

## Testing guide

### Local testing

1. Copy `local.settings.json.example` to `local.settings.json`
2. Start the function app:

   ```bash
   func start
   ```

3. Update `/samples/ConsentFunctionsClient.Console/appsettings.json`:
   - `BaseUrl`: `http://localhost:7071/api/`
   - `ApiKey`: your local or deployed function key
4. Run the sample client:

   ```bash
   dotnet run --project samples/ConsentFunctionsClient.Console/ConsentFunctionsClient.Console.csproj
   ```

### Build verification

```bash
dotnet build ConsentFunctionsApp.csproj
dotnet build samples/ConsentFunctionsClient.Console/ConsentFunctionsClient.Console.csproj
```

## Sample client project contents

The sample client project demonstrates:

- Configuration management through `appsettings.json`
- `HttpClientFactory` registration via dependency injection
- API key authentication with the `x-functions-key` header
- Full CRUD calls to the consent endpoints
- Centralized error handling through `FunctionAppApiException`
