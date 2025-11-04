using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System;
using System.Threading.Tasks;
using McpSamples.PptFontFix.HybridApp.Services;
using McpSamples.PptFontFix.HybridApp.Models;

using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Tools;

/// <summary>
/// This provides interface for the PPT font fix tool.
/// </summary>

public interface IPPTFontFixTool
{
    /// <summary>
    /// open a PPT file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>Returns the result as a string.</returns>
    Task<string> OpenPPTFileAsync(string filePath);

    /// <summary>
    /// Analyze fonts in a PPT file.
    /// </summary>
    /// <returns>Returns <see cref="PPTFontAnalyzeResult"/> instance.</returns>
    Task<PPTFontAnalyzeResult> AnalyzeFontsAsync();
}

/// <summary>
/// This represents the tool entity for PPT font fixing.
/// </summary>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
/// <param name="service"><see cref="IPPTFontFixService"/> instance.</param>
[McpServerToolType]
public class PPTFontFixTool(IPPTFontFixService service, ILogger<PPTFontFixTool> logger) : IPPTFontFixTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "open_ppt_file", Title = "Open a PPT File")]
    [Description("Opens a PPT file and verifies it using ShapeCrawler.")]
    public async Task<string> OpenPPTFileAsync(
        [Description("The path of the PPT file to open")] string filePath)
    {
        try
        {
            await service.OpenPPTFileAsync(filePath).ConfigureAwait(false);

            string successMessage = $"PPT file opened successfully: {filePath}";
            logger.LogInformation(successMessage);
            return successMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PPT file: {FilePath}", filePath);

            // throw exception to inform the caller about the failure
            throw;
        }
    }

    /// <inheritdoc />
    [McpServerTool(Name = "analyze_fonts", Title = "Analyze Fonts in PPT File")]
    [Description("Analyzes fonts used in the opened PPT file and identifies inconsistencies.")]
    public async Task<PPTFontAnalyzeResult> AnalyzeFontsAsync()
    {
        try
        {
            // call the service to analyze fonts
            PPTFontAnalyzeResult result = await service.AnalyzeFontsAsync().ConfigureAwait(false);

            if (result != null)
            {
                int count = result.InconsistentlyUsedFonts?.Count ?? 0;
                logger.LogInformation("Font analysis completed. Inconsistently used fonts count: {Count}", count);
                return result;
            }
            else
            {
                logger.LogWarning("Font analysis service returned a null result.");
                return new PPTFontAnalyzeResult();
            }
        }
        catch (Exception ex)
        {
            // log the error and rethrow
            logger.LogError(ex, "Failed to analyze fonts in the PPT file.");
            throw; // preserve stack trace
        }
    }
}