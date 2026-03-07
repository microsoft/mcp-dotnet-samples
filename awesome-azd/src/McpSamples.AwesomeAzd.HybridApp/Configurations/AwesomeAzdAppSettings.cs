using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.AwesomeAzd.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for awesome-azd app.
/// </summary>
public class AwesomeAzdAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Awesome Azd",
        Version = "1.0.0",
        Description = "A simple MCP server for searching and loading custom instructions from the awesome-azd repository."
    };
}
