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
    /// Opens the specified PPT file and loads it into memory for subsequent analysis.
    /// </summary>
    /// <param name="filePath">The path to the PPT file.</param>
    /// <returns>A string message indicating success or an action required instruction for the LLM.</returns>
    Task<string> OpenPptFileAsync(string filePath); 

    /// <summary>
    /// Analyzes fonts in the file currently loaded in memory.
    /// </summary>
    /// <returns>Returns <see cref="PptFontAnalyzeResult"/> instance.</returns>
    Task<PptFontAnalyzeResult> AnalyzeFontsAsync(); 

    /// <summary>
    /// Updates the PPT file by removing unused fonts, replacing inconsistent fonts, and saving to a new path.
    /// </summary>
    /// <param name="replacementFont">The font to replace all inconsistent fonts with.</param>
    /// <param name="inconsistentFontsToReplace">The list of inconsistent font names to be replaced.</param>
    /// <param name="locationsToRemove">The list of shape locations to be removed.</param>
    /// <param name="outputDirectory">The directory path on the host machine to save the modified file.</param>
    /// <param name="newFileName">The full path to save the new .pptx file.</param>
    /// <returns>Returns a success message with the new file path.</returns>
    Task<string> UpdatePptFileAsync(string replacementFont, List<string> inconsistentFontsToReplace, List<FontUsageLocation> locationsToRemove, string outputDirectory, string newFileName);
}

/// <summary>
/// This represents the tool entity for Ppt font fixing.
/// </summary>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
/// <param name="service"><see cref="IPptFontFixService"/> instance.</param>
[McpServerToolType]
public class PptFontFixTool(IPptFontFixService service, ILogger<PptFontFixTool> logger) : IPptFontFixTool
{
    [McpServerTool(Name = "open_ppt_file", Title = "Open PPT File")]
    [Description("Opens a Ppt file and loads it into memory. Returns action instructions if the file cannot be accessed.")]
    public async Task<string> OpenPptFileAsync(
        [Description("The path of the Ppt file to open and analyze")] string filePath)
    {
        string? actionRequiredMessage = await service.OpenPptFileAsync(filePath).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(actionRequiredMessage))
        {
            return actionRequiredMessage; 
        }

        logger.LogInformation("Ppt file opened successfully: {FilePath}", filePath);
        return $"PPT file '{filePath}' successfully loaded into memory. You can now call the analyze_fonts tool.";
    }


    /// <inheritdoc />
    [McpServerTool(Name = "analyze_fonts", Title = "Analyze Fonts")]
    [Description("Analyzes fonts used in the PPT file currently loaded in memory, identifying inconsistencies.")]
    public async Task<PptFontAnalyzeResult> AnalyzeFontsAsync()
    {
        try
        {
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
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Analysis failed because the PPT file was not loaded.");
            throw; 
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during font analysis process.");
            throw;
        }
    }

    /// <inheritdoc />
    [McpServerTool(Name = "update_ppt_file", Title = "Update and Save PPT File")]
    [Description("Removes unused fonts, replaces inconsistently used fonts with another font defined within the Ppt file, and saves the file to a user-specified path.")]
    public async Task<string> UpdatePptFileAsync(
        [Description("The replacement font")] string replacementFont,
        [Description("The fonts to be replaced")] List<string> inconsistentFontsToReplace,
        [Description("A list of shape locations (from analysis result) to be removed")] List<FontUsageLocation> locationsToRemove,
        [Description("The directory path on the host machine to save the modified file (e.g., C:\\Users\\Downloads)")] string outputDirectory,
        [Description("The filename for the modified Ppt file.")] string newFileName) 
    {
        try
        {
            int removalCount = await service.RemoveUnusedFontsAsync(locationsToRemove).ConfigureAwait(false);
            logger.LogInformation("{Count} unused font shapes removed.", removalCount);

            int totalReplacementCount = 0;
            if (inconsistentFontsToReplace != null)
            {
                foreach (var fontToReplace in inconsistentFontsToReplace)
                {
                    int count = await service.ReplaceFontAsync(fontToReplace, replacementFont).ConfigureAwait(false);
                    totalReplacementCount += count;
                }
            }
            logger.LogInformation("{Count} instances of inconsistent fonts replaced with '{ReplacementFont}'.", totalReplacementCount, replacementFont);

            string accessPath = await service.SavePptFileAsync(newFileName, outputDirectory).ConfigureAwait(false); 
            
            logger.LogInformation("Ppt file saved successfully: {Path}", accessPath);

            return $"PPT update complete. Removed: {removalCount}, Replaced: {totalReplacementCount}. Result: {accessPath}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during Ppt file update process. Save name: {FileName}", newFileName);
            throw;
        }
    }
}