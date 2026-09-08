using ConsentFunctionsApp.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Entry point / startup for the isolated-worker Azure Functions host.
// Configures dependency injection, including the singleton CosmosClient
// and the ICosmosDbService used by the HTTP triggered functions.
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

IConfiguration configuration = builder.Configuration;

string cosmosConnectionString = configuration["CosmosDb:ConnectionString"]
    ?? throw new InvalidOperationException(
        "Missing required configuration value 'CosmosDb:ConnectionString'. " +
        "Set it in local.settings.json (for local development) or as an application setting.");

string databaseName = configuration["CosmosDb:DatabaseName"] ?? "ConsentServiceDb";
string containerName = configuration["CosmosDb:ContainerName"] ?? "Consents";

// Register a single, long-lived CosmosClient instance as recommended by the Cosmos DB SDK guidance.
builder.Services.AddSingleton(_ =>
{
    var clientOptions = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };

    return new CosmosClient(cosmosConnectionString, clientOptions);
});

builder.Services.AddSingleton<ICosmosDbService>(serviceProvider =>
{
    var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
    var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CosmosDbService>>();
    return new CosmosDbService(cosmosClient, logger, databaseName, containerName);
});

builder.Build().Run();
