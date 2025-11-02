using System.ComponentModel;

using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.OpenApiToSdk.HybridApp.Services;

using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the OpenAPI to SDK tool.
/// </summary>
public interface IOpenApiToSdkTool
{
    /// <summary>
    /// Downloads OpenAPI specification from a URL.
    /// </summary>
    /// <param name="openApiUrl">The URL of the OpenAPI specification.</param>
    /// <returns>The downloaded OpenAPI specification content.</returns>
    Task<string> DownloadOpenApiSpecAsync(string openApiUrl);
}

/// <summary>
/// This represents the tool entity for OpenAPI to SDK operations.
/// </summary>
/// <param name="openApiService"><see cref="IOpenApiService"/> instance.</param>
public class OpenApiToSdkTool(IOpenApiService openApiService) : IOpenApiToSdkTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "download_openapi_spec")]
    [Description("Download OpenAPI specification from a URL")]
    public async Task<string> DownloadOpenApiSpecAsync(
        [Description("URL of the OpenAPI specification")] string openApiUrl)
    {
        return await openApiService.DownloadOpenApiSpecAsync(openApiUrl);
    }
}