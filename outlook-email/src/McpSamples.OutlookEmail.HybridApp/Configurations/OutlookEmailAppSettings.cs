using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.OutlookEmail.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for outlook-email app.
/// </summary>
public class OutlookEmailAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Outlook Email",
        Version = "1.0.0",
        Description = "A simple MCP server for sending emails through Outlook."
    };
}