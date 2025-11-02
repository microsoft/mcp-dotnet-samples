namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This represents the service for OpenAPI operations.
/// </summary>
/// <param name="httpClient"><see cref="HttpClient"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TOpenApiService}"/> instance.</param>
public class OpenApiService(HttpClient httpClient, ILogger<OpenApiService> logger) : IOpenApiService
{
    /// <inheritdoc />
    public async Task<string> DownloadOpenApiSpecAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Downloading OpenAPI spec from {Url}", url);
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Successfully downloaded OpenAPI spec ({Length} characters)", content.Length);

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download OpenAPI spec from {Url}", url);
            throw;
        }
    }
}