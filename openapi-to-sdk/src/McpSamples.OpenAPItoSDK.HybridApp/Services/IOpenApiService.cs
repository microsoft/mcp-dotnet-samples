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
}