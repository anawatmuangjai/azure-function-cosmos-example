using System.Net;
using ConsentFunctionsApp.Models;
using ConsentFunctionsApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConsentFunctionsApp.Functions;

/// <summary>
/// HTTP triggered function that retrieves a single consent record by id.
/// </summary>
public class GetConsentFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<GetConsentFunction> _logger;

    public GetConsentFunction(ICosmosDbService cosmosDbService, ILogger<GetConsentFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/consents/{id}?userId={userId}
    /// The optional "userId" query string parameter is used as the Cosmos DB partition key
    /// to avoid a cross-partition query when it is known by the caller.
    /// Response: 200 OK with the Consent document, or 404 Not Found.
    /// </summary>
    [Function("GetConsent")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "consents/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            string? userId = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query)
                .TryGetValue("userId", out var userIdValues) ? userIdValues.ToString() : null;

            Consent? consent = await _cosmosDbService.GetConsentAsync(id, userId, cancellationToken);

            if (consent is null)
            {
                _logger.LogInformation("Consent {ConsentId} was not found.", id);
                HttpResponseData notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = $"Consent '{id}' was not found." });
                return notFound;
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(consent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting consent {ConsentId}.", id);
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            return errorResponse;
        }
    }
}
