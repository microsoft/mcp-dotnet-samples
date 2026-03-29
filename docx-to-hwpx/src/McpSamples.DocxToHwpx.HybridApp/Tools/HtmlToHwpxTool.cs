using System.ComponentModel;

using CSnakes.Runtime;

using McpSamples.DocxToHwpx.HybridApp.Configurations;

using ModelContextProtocol.Server;

namespace McpSamples.DocxToHwpx.HybridApp.Tools;

/// <summary>
/// This represents the tool entity for converting .html to .hwpx.
/// </summary>
/// <param name="settings"><see cref="DocxToHwpxAppSettings"/> instance.</param>
/// <param name="pythonEnvironment"><see cref="IPythonEnvironment"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class HtmlToHwpxTool(DocxToHwpxAppSettings settings, IPythonEnvironment pythonEnvironment, ILogger<HtmlToHwpxTool> logger) : IDocumentToHwpxTool
{
    private readonly DocxToHwpxAppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly IPythonEnvironment _pythonEnvironment = pythonEnvironment ?? throw new ArgumentNullException(nameof(pythonEnvironment));
    private readonly ILogger<HtmlToHwpxTool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    [McpServerTool(Name = "convert_html_to_hwpx", Title = "Convert .html to .hwpx")]
    [Description("Converts .html file to .hwpx file.")]
    public async Task<string> ConvertAsync(
        [Description("The input .html filepath")] string? input,
        [Description("The output .hwpx filepath")] string? output,
        [Description("The reference template filepath")] string? reference = null)
    {
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            throw new FileNotFoundException($"Input file not found: {input}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("Output filepath must be specified.", nameof(output));
        }

        this._logger.LogInformation("Converting .html to .hwpx: {Input} -> {Output}", input, output);

        var converter = this._pythonEnvironment.Converter();

        var referenceHwpx = !string.IsNullOrWhiteSpace(reference) && File.Exists(reference)
            ? reference
            : converter.GetDefaultReference();

        if (string.IsNullOrWhiteSpace(referenceHwpx))
        {
            throw new FileNotFoundException("No reference HWPX file found. Pass an explicit path via 'reference' or ensure pypandoc-hwpx blank.hwpx exists.");
        }

        var result = converter.ConvertToHwpx(input, output, referenceHwpx);

        this._logger.LogInformation("Conversion complete: {Result}", result);

        return await Task.FromResult(result);
    }
}
