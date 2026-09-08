using System.Net;
using ConsentFunctionsApp.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConsentFunctionsApp.Functions;

/// <summary>
/// HTTP triggered function that deletes a consent record.
/// </summary>
public class DeleteConsentFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<DeleteConsentFunction> _logger;

    public DeleteConsentFunction(ICosmosDbService cosmosDbService, ILogger<DeleteConsentFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    /// <summary>
    /// DELETE /api/consents/{id}?userId={userId}
    /// Response: 204 No Content on success, or 404 Not Found.
    /// </summary>
    [Function("DeleteConsent")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "consents/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            string? userId = QueryHelpers.ParseQuery(req.Url.Query)
                .TryGetValue("userId", out var userIdValues) ? userIdValues.ToString() : null;

            bool deleted = await _cosmosDbService.DeleteConsentAsync(id, userId, cancellationToken);

            if (!deleted)
            {
                _logger.LogInformation("Consent {ConsentId} was not found for delete.", id);
                HttpResponseData notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = $"Consent '{id}' was not found." });
                return notFound;
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting consent {ConsentId}.", id);
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            return errorResponse;
        }
    }
}
