using ConsentFunctionsClient.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsentFunctionsClient(builder.Configuration);
builder.Services.AddTransient<ConsentWorkflow>();

using IHost host = builder.Build();

await host.Services.GetRequiredService<ConsentWorkflow>().RunAsync();

internal sealed class ConsentWorkflow
{
    private readonly IConsentFunctionsClient _client;
    private readonly ILogger<ConsentWorkflow> _logger;

    public ConsentWorkflow(IConsentFunctionsClient client, ILogger<ConsentWorkflow> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Consent created = await _client.CreateConsentAsync(
                new CreateConsentRequest("user-123", "marketing", true),
                cancellationToken);

            _logger.LogInformation("Created consent {ConsentId} for user {UserId}.", created.Id, created.UserId);

            Consent? existing = await _client.GetConsentAsync(created.Id, created.UserId, cancellationToken);
            _logger.LogInformation("Fetched consent {ConsentId}: {ConsentType} = {IsAgreed}.",
                existing?.Id,
                existing?.ConsentType,
                existing?.IsAgreed);

            PagedConsentsResponse page = await _client.ListConsentsAsync(pageSize: 10, cancellationToken: cancellationToken);
            _logger.LogInformation("Retrieved {Count} consent records. Continuation token present: {HasToken}.",
                page.Items.Count,
                !string.IsNullOrWhiteSpace(page.ContinuationToken));

            Consent updated = await _client.UpdateConsentAsync(
                created.Id,
                new UpdateConsentRequest("marketing", false),
                created.UserId,
                cancellationToken);

            _logger.LogInformation("Updated consent {ConsentId} to IsAgreed={IsAgreed}.", updated.Id, updated.IsAgreed);

            await _client.DeleteConsentAsync(created.Id, created.UserId, cancellationToken);
            _logger.LogInformation("Deleted consent {ConsentId}.", created.Id);
        }
        catch (FunctionAppApiException ex)
        {
            _logger.LogError("Azure Function returned {StatusCode}: {ResponseBody}", (int)ex.StatusCode, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling the Azure Functions app.");
        }
    }
}
