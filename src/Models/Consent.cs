using System.Text.Json.Serialization;

namespace ConsentFunctionsApp.Models;

/// <summary>
/// Represents a user consent record stored in Azure Cosmos DB.
/// The <see cref="UserId"/> property is used as the partition key for the container.
/// </summary>
public class Consent
{
    /// <summary>
    /// Unique identifier of the consent record. Also used as the Cosmos DB document id.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Identifier of the user this consent belongs to. Used as the Cosmos DB partition key.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Type/category of the consent (e.g. "marketing", "terms-of-service", "data-processing").
    /// </summary>
    [JsonPropertyName("consentType")]
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user has agreed (true) or declined/withdrawn (false) this consent.
    /// </summary>
    [JsonPropertyName("isAgreed")]
    public bool IsAgreed { get; set; }

    /// <summary>
    /// UTC timestamp of when the consent record was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of when the consent record was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
