using System.ComponentModel;

using McpSamples.DocxToHwpx.HybridApp.Configurations;

using ModelContextProtocol.Server;

namespace McpSamples.DocxToHwpx.HybridApp.Tools;

/// <summary>
/// This represents the tool entity for converting .docx to .hwpx.
/// </summary>
/// <param name="settings"><see cref="DocxToHwpxAppSettings"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class MarkdownToHwpxTool(DocxToHwpxAppSettings settings, ILogger<MarkdownToHwpxTool> logger) : IDocumentToHwpxTool
{
    private readonly DocxToHwpxAppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ILogger<MarkdownToHwpxTool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    [McpServerTool(Name = "convert_md_to_hwpx", Title = "Convert .md to .hwpx")]
    [Description("Converts .md file to .hwpx file.")]
    public async Task<string> ConvertAsync(
        [Description("The input .md filepath")] string? input,
        [Description("The output .hwpx filepath")] string? output,
        [Description("The reference template filepath")] string? reference = null)
    {
        throw new NotImplementedException("This is a sample code. Please implement the conversion logic here.");
    }
}
