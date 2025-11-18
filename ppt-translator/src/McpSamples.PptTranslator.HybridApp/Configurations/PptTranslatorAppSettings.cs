using McpSamples.Shared.Configurations;
using Microsoft.OpenApi.Models;

namespace McpSamples.PptTranslator.HybridApp.Configurations;

/// <summary>
/// Application settings for the PPT Translator service.
/// </summary>
public class PptTranslatorAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP PPT Translator",
        Version = "1.0.0",
        Description = "MCP server for translating PowerPoint (.pptx) files."
    };
}
