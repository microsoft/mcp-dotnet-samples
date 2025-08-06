using Microsoft.OpenApi.Models;

namespace McpSamples.Shared.Configurations;

/// <summary>
/// This represents the base class for application settings.
/// </summary>
public class TodoListAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Todo Management",
        Version = "1.0.0",
        Description = "A simple MCP server for managing todo list items."
    };
}
