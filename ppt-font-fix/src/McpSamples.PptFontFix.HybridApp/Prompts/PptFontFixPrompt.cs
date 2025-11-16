using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for PPT Font Fix prompts.
/// </summary>
public interface IPptFontFixPrompt
{
    /// <summary>
    /// Gets a prompt to call the 'analyze_ppt_file' tool.
    /// </summary>
    /// <param name="filePath">The full path to the .pptx file to be analyzed.</param>
    /// <returns>A formatted prompt to start the font analysis.</returns>
    string GetAnalysisPrompt(string filePath);

    /// <summary>
    /// Gets a prompt to call the 'update_ppt_file' tool.
    /// </summary>
    /// <param name="replacementFont">The font to replace all inconsistent fonts with.</param>
    /// <param name="newFilePath">The path to save the new file.</param>
    /// <returns>A formatted prompt to apply all fixes and save.</returns>
    string GetUpdatePrompt(string replacementFont, string newFilePath);
}

/// <summary>
/// This represents the prompts entity for the PptFontFix 2-Tool system.
/// </summary>
[McpServerPromptType]
public class PptFontFixPrompt : IPptFontFixPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "run_analysis", Title = "Prompt to run PPT font analysis tool")]
    [Description("Get a prompt to call the 'analyze_ppt_file' tool, which opens and analyzes a PPT file.")]
    public string GetAnalysisPrompt(
        [Description("The full path to the .pptx file to be analyzed")] string filePath)
    {
        return $"""
        Please start the PPT font analysis process.

        Here's the process to follow:

        1.  Call the `analyze_ppt_file` tool using the provided file path: `{filePath}`.
        2.  Receive the `PptFontAnalyzeResult` object from the tool.
        3.  **Store this entire `PptFontAnalyzeResult` object in your context.** (This is essential for the update step).
        4.  Present the `UsedFonts` and `InconsistentlyUsedFonts` lists from the result to the user.
        5.  Ask the user to choose one (1) font from the `UsedFonts` list to be the new standard font.
        """;
    }

    /// <inheritdoc />
    [McpServerPrompt(Name = "run_update_and_save", Title = "Prompt to run PPT font update tool")]
    [Description("Get a prompt to call the 'update_ppt_file' tool, which fixes fonts and saves the file.")]
    public string GetUpdatePrompt(
        [Description("The chosen standard font to replace all inconsistent fonts")] string replacementFont,
        [Description("The full path to save the new .pptx file")] string newFilePath)
    {
        return $"""
        Please apply all font fixes and save the presentation.

        Here's the process to follow:

        1.  **Recall the `PptFontAnalyzeResult` object** stored in your context from the analysis step.
        2.  Extract the following lists from the recalled object:
            * `UsedFonts`
            * `InconsistentlyUsedFonts`
            * `UnusedFontLocations`
        3.  **Validate:** Check if the user's chosen `replacementFont` ("{replacementFont}") is present in the `UsedFonts` list.
        4.  **If validation fails:** STOP. Inform the user that "{replacementFont}" is not a valid standard font and ask them to choose from the `UsedFonts` list again.
        5.  **If validation succeeds:** Call the `update_ppt_file` tool with the following four arguments:
            * `replacementFont`: "{replacementFont}"
            * `inconsistentFontsToReplace`: The `InconsistentlyUsedFonts` list (from context)
            * `locationsToRemove`: The `UnusedFontLocations` list (from context)
            * `newFilePath`: "{newFilePath}"
        6.  The tool will return a final success string. Present this string directly to the user.
        """;
    }
}