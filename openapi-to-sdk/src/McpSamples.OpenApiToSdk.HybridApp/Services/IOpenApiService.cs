using McpSamples.OpenApiToSdk.HybridApp.Models;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This provides interfaces for the OpenAPI service.
/// </summary>
public interface IOpenApiService
{
    /// <summary>
    /// Downloads OpenAPI specification from a URL.
    /// </summary>
    /// <param name="openApiUrl">The URL of the OpenAPI specification.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The content of the OpenAPI specification as a string.</returns>
    Task<string> DownloadOpenApiSpecAsync(string openApiUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a client SDK from an OpenAPI specification.
    /// </summary>
    /// <param name="specSource">The URL or raw content of the OpenAPI specification.</param>
    /// <param name="language">The target programming language for the SDK.</param>
    /// <param name="className">The name for the main client class (optional).</param>
    /// <param name="namespaceName">The namespace for the generated SDK (optional).</param>
    /// <param name="additionalOptions">Additional options to pass to the Kiota CLI (optional).</param>
    /// <returns>An <see cref="OpenApiToSdkResult"/> object containing the result of the SDK generation.</returns>
    Task<OpenApiToSdkResult> GenerateSdkAsync(
        string specSource,
        string language,
        string? className = null,
        string? namespaceName = null,
        string? additionalOptions = null);

    /// <summary>
    /// Runs the Kiota command-line tool with the specified arguments.
    /// </summary>
    /// <param name="openApiSpecPath">The path to the OpenAPI specification file or a URL.</param>
    /// <param name="language">The target programming language.</param>
    /// <param name="outputDir">The directory where the generated SDK will be saved.</param>
    /// <param name="additionalOptions">Additional options to pass to the Kiota CLI (optional).</param>
    /// <returns>An error message if the command fails; otherwise, <c>null</c>.</returns>
    Task<string?> RunKiotaAsync(string openApiSpecPath, string language, string outputDir, string? additionalOptions = null);
}