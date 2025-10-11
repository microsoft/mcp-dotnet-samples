using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.TodoList.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for ppt-font-fix app.
/// </summary>
public class PPTFontFixAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP PPT Font Fix",
        Version = "1.0.0",
        Description = "A simple MCP server for managing PPT font fixing."
    };
}
