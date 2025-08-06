using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.TodoList.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for todo-list app.
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
