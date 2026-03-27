using System.ComponentModel;

using McpSamples.DocxToHwpx.HybridApp.Configurations;

using ModelContextProtocol.Server;

namespace McpSamples.DocxToHwpx.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the .docx to .hwpx tool.
/// </summary>
public interface IDocxToHwpxTool
{
    /// <summary>
    /// Converts .docx file to .hwpx file.
    /// </summary>
    /// <param name="docx">The .docx filepath.</param>
    /// <param name="hwpx">The .hwpx filepath.</param>
    /// <param name="template">The template filepath.</param>
    /// <returns>The converted .hwpx text.</returns>
    Task<string> ConvertAsync(string? docx, string? hwpx, string? template = null);
}

/// <summary>
/// This represents the tool entity for converting .docx to .hwpx.
/// </summary>
/// <param name="settings"><see cref="DocxToHwpxAppSettings"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class DocxToHwpxTool(DocxToHwpxAppSettings settings, ILogger<DocxToHwpxTool> logger) : IDocxToHwpxTool
{
    private readonly DocxToHwpxAppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ILogger<DocxToHwpxTool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    [McpServerTool(Name = "convert_docx_to_hwpx", Title = "Convert .docx to .hwpx")]
    [Description("Converts .docx file to .hwpx file.")]
    public async Task<string> ConvertAsync(
        [Description("The .docx filepath")] string? docx,
        [Description("The .hwpx filepath")] string? hwpx,
        [Description("The template filepath")] string? template = null)
    {
        throw new NotImplementedException("This is a sample code. Please implement the conversion logic here.");
    }
}
