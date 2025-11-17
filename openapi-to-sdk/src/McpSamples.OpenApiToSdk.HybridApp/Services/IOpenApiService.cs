namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This provides interfaces for OpenAPI service operations.
/// </summary>
public interface IOpenApiService
{
    /// <summary>
    /// Downloads OpenAPI specification from a URL.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The downloaded content as a string.</returns>
    Task<string> DownloadOpenApiSpecAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Kiota CLI with the specified options.
    /// </summary>
    /// <param name="openApiSpecPath">Path to the OpenAPI spec file.</param>
    /// <param name="language">Target language for the SDK.</param>
    /// <param name="outputDir">Output directory for the generated SDK.</param>
    /// <param name="additionalOptions">Additional Kiota options.</param>
    /// <returns>Error message if failed, null if successful.</returns>
    Task<string?> RunKiotaAsync(string openApiSpecPath, string language, string outputDir, string? additionalOptions = null);
}