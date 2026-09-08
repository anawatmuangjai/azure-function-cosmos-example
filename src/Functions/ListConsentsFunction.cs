using System.Net;
using ConsentFunctionsApp.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConsentFunctionsApp.Functions;

/// <summary>
/// HTTP triggered function that lists consent records with simple continuation-token pagination.
/// </summary>
public class ListConsentsFunction
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<ListConsentsFunction> _logger;

    public ListConsentsFunction(ICosmosDbService cosmosDbService, ILogger<ListConsentsFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/consents?pageSize={pageSize}&amp;continuationToken={token}
    /// Response: 200 OK with { items: Consent[], continuationToken: string|null }
    /// </summary>
    [Function("ListConsents")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "consents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = QueryHelpers.ParseQuery(req.Url.Query);

            int pageSize = DefaultPageSize;
            if (query.TryGetValue("pageSize", out var pageSizeValues) &&
                int.TryParse(pageSizeValues.ToString(), out int parsedPageSize) &&
                parsedPageSize > 0)
            {
                pageSize = Math.Min(parsedPageSize, MaxPageSize);
            }

            string? continuationToken = query.TryGetValue("continuationToken", out var tokenValues)
                ? tokenValues.ToString()
                : null;

            (var items, var nextContinuationToken) = await _cosmosDbService.ListConsentsAsync(
                pageSize, continuationToken, cancellationToken);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                items,
                continuationToken = nextContinuationToken
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while listing consents.");
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            return errorResponse;
        }
    }
}
