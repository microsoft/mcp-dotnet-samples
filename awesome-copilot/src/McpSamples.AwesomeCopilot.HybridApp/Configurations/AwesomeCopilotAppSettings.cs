using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.AwesomeCopilot.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for awesome-copilot app.
/// </summary>
public class AwesomeCopilotAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Awesome Copilot",
        Version = "1.0.0",
        Description = "A simple MCP server for searching and loading custom instructions from the awesome-copilot repository."
    };
}
