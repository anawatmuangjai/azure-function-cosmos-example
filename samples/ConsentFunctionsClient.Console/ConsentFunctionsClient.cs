using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ConsentFunctionsClient.Sample;

public sealed class FunctionAppClientOptions
{
    public const string SectionName = "FunctionApp";

    public string BaseUrl { get; set; } = "https://your-function-app.azurewebsites.net/api/";

    public string ApiKey { get; set; } = "YOUR_FUNCTION_KEY";
}

public sealed class Consent
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string ConsentType { get; set; } = string.Empty;

    public bool IsAgreed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed record CreateConsentRequest(string UserId, string ConsentType, bool IsAgreed);

public sealed record UpdateConsentRequest(string ConsentType, bool IsAgreed);

public sealed class PagedConsentsResponse
{
    public IReadOnlyList<Consent> Items { get; set; } = Array.Empty<Consent>();

    public string? ContinuationToken { get; set; }
}

public interface IConsentFunctionsClient
{
    Task<Consent> CreateConsentAsync(CreateConsentRequest request, CancellationToken cancellationToken = default);

    Task<Consent?> GetConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default);

    Task<PagedConsentsResponse> ListConsentsAsync(int pageSize = 20, string? continuationToken = null, CancellationToken cancellationToken = default);

    Task<Consent> UpdateConsentAsync(string id, UpdateConsentRequest request, string? userId = null, CancellationToken cancellationToken = default);

    Task DeleteConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default);
}

public sealed class FunctionAppApiException : Exception
{
    public FunctionAppApiException(HttpStatusCode statusCode, string responseBody)
        : base($"Function app request failed with status code {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }
}

public sealed class ConsentFunctionsClient : IConsentFunctionsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly FunctionAppClientOptions _options;

    public ConsentFunctionsClient(HttpClient httpClient, IOptions<FunctionAppClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<Consent> CreateConsentAsync(CreateConsentRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "consents", CreateJsonContent(request));
        return await SendForJsonAsync<Consent>(message, cancellationToken);
    }

    public async Task<Consent?> GetConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, BuildConsentUri(id, userId));
        return await SendForJsonAsync<Consent>(message, cancellationToken);
    }

    public async Task<PagedConsentsResponse> ListConsentsAsync(
        int pageSize = 20,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        string requestUri = $"consents?pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            requestUri += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
        }

        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, requestUri);
        return await SendForJsonAsync<PagedConsentsResponse>(message, cancellationToken);
    }

    public async Task<Consent> UpdateConsentAsync(
        string id,
        UpdateConsentRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, BuildConsentUri(id, userId), CreateJsonContent(request));
        return await SendForJsonAsync<Consent>(message, cancellationToken);
    }

    public async Task DeleteConsentAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Delete, BuildConsentUri(id, userId));
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return;
        }

        await ThrowIfNotSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri, HttpContent? content = null)
    {
        var message = new HttpRequestMessage(method, requestUri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.Add("x-functions-key", _options.ApiKey);
        }

        if (content is not null)
        {
            message.Content = content;
        }

        return message;
    }

    private async Task<T> SendForJsonAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken);
        await ThrowIfNotSuccessAsync(response, cancellationToken);

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        T? payload = await JsonSerializer.DeserializeAsync<T>(responseStream, JsonOptions, cancellationToken);

        return payload ?? throw new InvalidOperationException("The function app returned an empty JSON response.");
    }

    private static async Task ThrowIfNotSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new FunctionAppApiException(response.StatusCode, responseBody);
    }

    private static StringContent CreateJsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private static string BuildConsentUri(string id, string? userId)
    {
        string requestUri = $"consents/{Uri.EscapeDataString(id)}";
        if (!string.IsNullOrWhiteSpace(userId))
        {
            requestUri += $"?userId={Uri.EscapeDataString(userId)}";
        }

        return requestUri;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsentFunctionsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FunctionAppClientOptions>()
            .Bind(configuration.GetSection(FunctionAppClientOptions.SectionName));

        services.AddHttpClient<IConsentFunctionsClient, ConsentFunctionsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FunctionAppClientOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
}
