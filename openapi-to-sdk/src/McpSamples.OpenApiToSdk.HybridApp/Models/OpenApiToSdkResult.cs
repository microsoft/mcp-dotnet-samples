namespace McpSamples.OpenApiToSdk.HybridApp.Models;

/// <summary>
/// Represents the result of an SDK generation operation.
/// </summary>
public class OpenApiToSdkResult
{
    /// <summary>
    /// Gets or sets the accessible path or URL to the generated ZIP file.
    /// (e.g., "http://localhost:8080/generated/sdk.zip" or "C:\...\sdk.zip")
    /// </summary>
    public string? ZipPath { get; set; }

    /// <summary>
    /// Gets or sets the absolute internal file path on the server.
    /// Useful for debugging or server-side logs.
    /// </summary>
    public string? ServerFilePath { get; set; }

    /// <summary>
    /// Gets or sets a user-friendly message describing the outcome.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}