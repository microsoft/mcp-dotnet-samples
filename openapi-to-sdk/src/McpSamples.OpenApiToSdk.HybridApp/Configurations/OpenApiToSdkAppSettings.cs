using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.OpenApiToSdk.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for openapi-to-sdk app.
/// </summary>
public class OpenApiToSdkAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP OpenAPI to SDK",
        Version = "1.0.0",
        Description = "A simple MCP server for integrating Kiota to generate an SDK from OpenAPI documents."
    };
}