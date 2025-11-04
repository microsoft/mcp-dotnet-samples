using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System;
using System.Threading.Tasks;
using McpSamples.PptFontFix.HybridApp.Services;
using McpSamples.PptFontFix.HybridApp.Models;

using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Tools;

/// <summary>
/// This provides interface for the Ppt font fix tool.
/// </summary>

public interface IPptFontFixTool
{
    /// <summary>
    /// open a Ppt file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>Returns the result as a string.</returns>
    Task<string> OpenPptFileAsync(string filePath);

    /// <summary>
    /// Analyze fonts in a Ppt file.
    /// </summary>
    /// <returns>Returns <see cref="PptFontAnalyzeResult"/> instance.</returns>
    Task<PptFontAnalyzeResult> AnalyzeFontsAsync();
}

/// <summary>
/// This represents the tool entity for Ppt font fixing.
/// </summary>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
/// <param name="service"><see cref="IPptFontFixService"/> instance.</param>
[McpServerToolType]
public class PptFontFixTool(IPptFontFixService service, ILogger<PptFontFixTool> logger) : IPptFontFixTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "open_ppt_file", Title = "Open a Ppt File")]
    [Description("Opens a Ppt file and verifies it using ShapeCrawler.")]
    public async Task<string> OpenPptFileAsync(
        [Description("The path of the Ppt file to open")] string filePath)
    {
        try
        {
            await service.OpenPptFileAsync(filePath).ConfigureAwait(false);

            string successMessage = $"Ppt file opened successfully: {filePath}";
            logger.LogInformation(successMessage);
            return successMessage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open Ppt file: {FilePath}", filePath);

            // throw exception to inform the caller about the failure
            throw;
        }
    }

    /// <inheritdoc />
    [McpServerTool(Name = "analyze_fonts", Title = "Analyze Fonts in Ppt File")]
    [Description("Analyzes fonts used in the opened Ppt file and identifies inconsistencies.")]
    public async Task<PptFontAnalyzeResult> AnalyzeFontsAsync()
    {
        try
        {
            // call the service to analyze fonts
            PptFontAnalyzeResult result = await service.AnalyzeFontsAsync().ConfigureAwait(false);

            if (result != null)
            {
                int count = result.InconsistentlyUsedFonts?.Count ?? 0;
                logger.LogInformation("Font analysis completed. Inconsistently used fonts count: {Count}", count);
                return result;
            }
            else
            {
                logger.LogWarning("Font analysis service returned a null result.");
                return new PptFontAnalyzeResult();
            }
        }
        catch (Exception ex)
        {
            // log the error and rethrow
            logger.LogError(ex, "Failed to analyze fonts in the Ppt file.");
            throw; // preserve stack trace
        }
    }
}