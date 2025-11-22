using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for PPT Font Fix prompts.
/// </summary>
public interface IPptFontFixPrompt
{
    /// <summary>
    /// Gets a prompt to start the PPT font fix workflow.
    /// </summary>
    /// <param name="filePath">The full path to the .pptx file to be analyzed.</param>
    /// <returns>A formatted prompt to start the font analysis.</returns>
    string GetAnalysisPrompt(string filePath);
}

/// <summary>
/// This represents the prompts entity for the PptFontFix system.
/// </summary>
[McpServerPromptType]
public class PptFontFixPrompt : IPptFontFixPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "fix_ppt_fonts", Title = "Start PPT Font Fix Workflow")]
    [Description("Analyzes the PPT file and guides the user through the font replacement process.")]
    public string GetAnalysisPrompt(
        [Description("The full path to the .pptx file (e.g. /files/test.pptx)")] string filePath)
    {
        return $"""
        You are an expert assistant for fixing PowerPoint font issues.
        Please execute the following workflow step-by-step:

        ### PHASE 1: ANALYSIS
        1. Call the `analyze_ppt_file` tool with the path: `{filePath}`.
        2. Analyze the result (`PptFontAnalyzeResult`).
        3. **Show the user** the list of `UsedFonts` and `InconsistentlyUsedFonts`.
        4. **Check `UnusedFontLocations`**:
           - If `UnusedFontLocations` is empty, skip the option selection and just ask for the Standard Font.
           - If `UnusedFontLocations` has items, **Ask the user to make TWO choices**:
             
             **A. Select a Standard Font:** (Choose one from `UsedFonts`)
             
             **B. Select an Action Mode:**
             1. **[Fix & Clean]**: Replace fonts AND remove unused/invisible text boxes (Specify the count of items found in `UnusedFontLocations`).
             2. **[Fix Only]**: Replace fonts ONLY. Keep unused text boxes as is.

        ### PHASE 2: UPDATE (Wait for user input)
        Once the user replies with their choices, proceed to call `update_ppt_file`.
        
        **Parameter Logic:**
        - `replacementFont`: The font selected by the user.
        - `inconsistentFontsToReplace`: The list of inconsistent fonts found in Phase 1.
        - `newFileName`: Generate a safe name like "result_fixed.pptx".
        
        **Critical Logic for `locationsToRemove`:**
        - IF User chose **1 (Fix & Clean)**: Pass the `UnusedFontLocations` list found in Phase 1.
        - IF User chose **2 (Fix Only)**: Pass an **empty list `[]`**. (Do NOT pass null, pass an empty JSON array).

        ### PHASE 3: RESULT PRESENTATION (CRITICAL!)
        The `update_ppt_file` tool will return a result string. Analyze it carefully.

        **CASE A: The result starts with `http` (Web Mode)**
        - Render it as a Clickable Markdown Link.
        - Format: `👉 [Click here to Download Fixed File](URL)`

        **CASE B: The result is a File Path (e.g. `/files/...` or `C:\...`) (Stdio/Local Mode)**
        - The user is using VS Code. VS Code automatically detects absolute paths.
        - **DO NOT use Markdown syntax** (No `[]` or `()`).
        - **DO NOT shorten the path.** Output the full string exactly as returned by the tool.
        - Output format:
          
          ✅ **Task Completed.**
          
          **File:**
          [Insert Raw Full Path Here]
        """;
    }
}