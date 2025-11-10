using McpSamples.Shared.Configurations;
using Microsoft.OpenApi.Models;

namespace McpSamples.PptTranslator.HybridApp.Configurations;
/// <summary>
/// This represents the application settings for ppt-translator service.
/// </summary>
public class PptTranslatorAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP PPT Translator",
        Version = "1.0.0",
        Description = "A simple MCP server for Translating PPTX file."
    };

}