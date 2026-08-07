using System.Text.Json.Serialization;

namespace McpSamples.OpenApiToSdk.HybridApp.Models;

/// <summary>
/// Represents the result of an SDK generation operation.
/// </summary>
public class OpenApiToSdkResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the generation was successful.
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the message or output path.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download URL (if applicable).
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}