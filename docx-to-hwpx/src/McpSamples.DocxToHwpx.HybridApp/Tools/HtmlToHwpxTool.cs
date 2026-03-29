using System.ComponentModel;

using McpSamples.DocxToHwpx.HybridApp.Configurations;

using ModelContextProtocol.Server;

namespace McpSamples.DocxToHwpx.HybridApp.Tools;

/// <summary>
/// This represents the tool entity for converting .html to .hwpx.
/// </summary>
/// <param name="settings"><see cref="DocxToHwpxAppSettings"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class HtmlToHwpxTool(DocxToHwpxAppSettings settings, ILogger<HtmlToHwpxTool> logger) : IDocumentToHwpxTool
{
    private readonly DocxToHwpxAppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ILogger<HtmlToHwpxTool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    [McpServerTool(Name = "convert_html_to_hwpx", Title = "Convert .html to .hwpx")]
    [Description("Converts .html file to .hwpx file.")]
    public async Task<string> ConvertAsync(
        [Description("The input .html filepath")] string? input,
        [Description("The output .hwpx filepath")] string? output,
        [Description("The reference template filepath")] string? reference = null)
    {
        throw new NotImplementedException("This is a sample code. Please implement the conversion logic here.");
    }
}
