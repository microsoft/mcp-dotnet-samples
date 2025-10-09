using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.OpenAPItoSDK.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for markdown-to-html app.
/// </summary>
public class OpenAPItoSDKAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP OpenAPI to SDK",
        Version = "1.0.0",
        Description = "A simple MCP server for integrating Kiota to generate an SDK from OpenAPI documents."
    };