namespace McpSamples.OpenApiToSdk.HybridApp.Models;

/// <summary>
/// Represents the result of a download operation.
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// Gets or sets the downloaded content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
