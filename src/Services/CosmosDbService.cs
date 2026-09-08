using ConsentFunctionsApp.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ConsentFunctionsApp.Services;

/// <summary>
/// Provides CRUD access to consent records stored in Azure Cosmos DB.
/// </summary>
public interface ICosmosDbService
{
    Task<Consent> CreateConsentAsync(Consent consent, CancellationToken cancellationToken = default);

    Task<Consent?> GetConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Consent> Items, string? ContinuationToken)> ListConsentsAsync(
        int maxItemCount = 20, string? continuationToken = null, CancellationToken cancellationToken = default);

    Task<Consent?> UpdateConsentAsync(string id, Consent consent, string? userId = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cosmos DB (SQL API) implementation of <see cref="ICosmosDbService"/> for the Consent container.
/// </summary>
public class CosmosDbService : ICosmosDbService
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosClient cosmosClient, ILogger<CosmosDbService> logger, string databaseName, string containerName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        _logger = logger;
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    /// <inheritdoc />
    public async Task<Consent> CreateConsentAsync(Consent consent, CancellationToken cancellationToken = default)
    {
        try
        {
            consent.CreatedAt = DateTime.UtcNow;
            consent.UpdatedAt = consent.CreatedAt;

            ItemResponse<Consent> response = await _container.CreateItemAsync(
                consent, new PartitionKey(consent.UserId), cancellationToken: cancellationToken);

            _logger.LogInformation("Created consent {ConsentId} for user {UserId}", consent.Id, consent.UserId);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to create consent for user {UserId}", consent.UserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Consent?> GetConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                ItemResponse<Consent> response = await _container.ReadItemAsync<Consent>(
                    id, new PartitionKey(userId), cancellationToken: cancellationToken);
                return response.Resource;
            }

            return await FindByIdAsync(id, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Consent {ConsentId} not found", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to get consent {ConsentId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Consent> Items, string? ContinuationToken)> ListConsentsAsync(
        int maxItemCount = 20, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _container.GetItemQueryIterator<Consent>(
                new QueryDefinition("SELECT * FROM c"),
                continuationToken,
                new QueryRequestOptions { MaxItemCount = maxItemCount });

            var items = new List<Consent>();
            string? nextContinuationToken = null;

            if (query.HasMoreResults)
            {
                FeedResponse<Consent> page = await query.ReadNextAsync(cancellationToken);
                items.AddRange(page);
                nextContinuationToken = page.ContinuationToken;
            }

            return (items, nextContinuationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to list consents");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Consent?> UpdateConsentAsync(string id, Consent consent, string? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Consent? existing = await GetConsentAsync(id, userId, cancellationToken);
            if (existing is null)
            {
                return null;
            }

            consent.Id = id;
            consent.UserId = existing.UserId;
            consent.CreatedAt = existing.CreatedAt;
            consent.UpdatedAt = DateTime.UtcNow;

            ItemResponse<Consent> response = await _container.ReplaceItemAsync(
                consent, id, new PartitionKey(existing.UserId), cancellationToken: cancellationToken);

            _logger.LogInformation("Updated consent {ConsentId} for user {UserId}", id, existing.UserId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Consent {ConsentId} not found", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to update consent {ConsentId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string? partitionKeyValue = userId;
            if (string.IsNullOrWhiteSpace(partitionKeyValue))
            {
                Consent? existing = await FindByIdAsync(id, cancellationToken);
                if (existing is null)
                {
                    return false;
                }

                partitionKeyValue = existing.UserId;
            }

            await _container.DeleteItemAsync<Consent>(id, new PartitionKey(partitionKeyValue), cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted consent {ConsentId} for user {UserId}", id, partitionKeyValue);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Consent {ConsentId} not found", id);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to delete consent {ConsentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Looks up a consent document by id when the partition key (userId) is not known,
    /// using a cross-partition query. Intended for the "GET/PUT/DELETE by id only" endpoints.
    /// </summary>
    private async Task<Consent?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        var query = _container.GetItemQueryIterator<Consent>(
            new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", id),
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        if (query.HasMoreResults)
        {
            FeedResponse<Consent> page = await query.ReadNextAsync(cancellationToken);
            return page.FirstOrDefault();
        }

        return null;
    }
}
