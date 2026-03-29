using McpSamples.Shared.Configurations;

using Microsoft.OpenApi;

namespace McpSamples.DocxToHwpx.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for docx-to-hwpx app.
/// </summary>
public class DocxToHwpxAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP .docx to .hwpx Converter",
        Version = "1.0.0",
        Description = "A simple MCP server for converting .docx to .hwpx."
    };
}
