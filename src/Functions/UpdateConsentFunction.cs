using System.Net;
using System.Text.Json;
using ConsentFunctionsApp.Models;
using ConsentFunctionsApp.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConsentFunctionsApp.Functions;

/// <summary>
/// HTTP triggered function that updates an existing consent record.
/// </summary>
public class UpdateConsentFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<UpdateConsentFunction> _logger;

    public UpdateConsentFunction(ICosmosDbService cosmosDbService, ILogger<UpdateConsentFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    /// <summary>
    /// PUT /api/consents/{id}?userId={userId}
    /// Request body example:
    /// {
    ///   "consentType": "marketing",
    ///   "isAgreed": false
    /// }
    /// Response: 200 OK with the updated Consent document, or 404 Not Found.
    /// </summary>
    [Function("UpdateConsent")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "consents/{id}")] HttpRequestData req,
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            Consent? consent = await req.ReadFromJsonAsync<Consent>(cancellationToken);

            if (consent is null || string.IsNullOrWhiteSpace(consent.ConsentType))
            {
                _logger.LogWarning("UpdateConsent received an invalid request body for {ConsentId}.", id);
                HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "ConsentType is required." });
                return badRequest;
            }

            string? userId = QueryHelpers.ParseQuery(req.Url.Query)
                .TryGetValue("userId", out var userIdValues) ? userIdValues.ToString() : null;

            Consent? updated = await _cosmosDbService.UpdateConsentAsync(id, consent, userId, cancellationToken);

            if (updated is null)
            {
                _logger.LogInformation("Consent {ConsentId} was not found for update.", id);
                HttpResponseData notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = $"Consent '{id}' was not found." });
                return notFound;
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(updated);
            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "UpdateConsent received malformed JSON for {ConsentId}.", id);
            HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid JSON payload." });
            return badRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating consent {ConsentId}.", id);
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            return errorResponse;
        }
    }
}
