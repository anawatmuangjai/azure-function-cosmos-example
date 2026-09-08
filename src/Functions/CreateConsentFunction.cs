using System.Net;
using System.Text.Json;
using ConsentFunctionsApp.Models;
using ConsentFunctionsApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConsentFunctionsApp.Functions;

/// <summary>
/// HTTP triggered function that creates a new consent record.
/// </summary>
public class CreateConsentFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<CreateConsentFunction> _logger;

    public CreateConsentFunction(ICosmosDbService cosmosDbService, ILogger<CreateConsentFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/consents
    /// Request body example:
    /// {
    ///   "userId": "user-123",
    ///   "consentType": "marketing",
    ///   "isAgreed": true
    /// }
    /// Response: 201 Created with the created Consent document.
    /// </summary>
    [Function("CreateConsent")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "consents")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            Consent? consent = await req.ReadFromJsonAsync<Consent>(cancellationToken);

            if (consent is null || string.IsNullOrWhiteSpace(consent.UserId) || string.IsNullOrWhiteSpace(consent.ConsentType))
            {
                _logger.LogWarning("CreateConsent received an invalid request body.");
                HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "UserId and ConsentType are required." });
                return badRequest;
            }

            // Ensure the id is freshly generated for a new record.
            consent.Id = Guid.NewGuid().ToString();

            Consent created = await _cosmosDbService.CreateConsentAsync(consent, cancellationToken);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(created);
            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "CreateConsent received malformed JSON.");
            HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid JSON payload." });
            return badRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating a consent.");
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            return errorResponse;
        }
    }
}
