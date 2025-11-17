namespace McpSamples.OpenApiToSdk.HybridApp.Models;

/// <summary>
/// Represents the result of an SDK generation operation.
/// </summary>
public class OpenApiToSdkResult
{
    /// <summary>
    /// Gets or sets the path to the generated ZIP file.
    /// This will be a local file path.
    /// </summary>
    public string? ZipPath { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
