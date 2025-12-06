using System.ComponentModel;
using ModelContextProtocol.Server;
using System.IO; 
using System;
using System.Collections.Generic;
using System.Linq; // Path.GetFileName을 위한 Last() 메서드 사용을 위해 필요

namespace McpSamples.PptFontFix.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for PPT Font Fix prompts.
/// </summary>
public interface IPptFontFixPrompt
{
    /// <summary>
    /// Gets a prompt to start the PPT font fix workflow.
    /// </summary>
    /// <param name="hostFilePath">The full path to the PPTX file on the host machine or a public URL.</param>
    string GetAnalysisPrompt(string hostFilePath);
}

/// <summary>
/// This represents the prompts entity for the PptFontFix system.
/// </summary>
[McpServerPromptType]
public class PptFontFixPrompt : IPptFontFixPrompt
{
   
    /// <inheritdoc />
    [McpServerPrompt(Name = "fix_ppt_fonts", Title = "Start PPT Font Fix Workflow")]
    [Description("Generate a structured workflow prompt for analyzing and fixing PPT fonts.")]
    public string GetAnalysisPrompt(
        [Description("The full path to the PPTX file or a public URL.")] string hostFilePath)
    {
        return $"""
        You are a professional PPT Font Fix helper.
        Your task is to open a PPT File, perform font analysis, modification, and storage based on this input (File Path : '{hostFilePath}'. 

        ### Order of execution
        1. Use the 'open_ppt_file' tool to verify that the file exists in FilePath. If the file opens without any problems, proceed to the next step.
        - If not, use the file system tool to move the file in the File Path that you suggested to a specific path based on the result value returned by the tool.
        2. If the file is opened normally, please analyze the usage status of the font in the PPT file through 'analyze_font' and show the following results.
        - List of used fonts
        - List of inconsistent used fons
        3. If an item exists in the unused fon:
        - Ask the user for two answers.
        A. **Choose a Standard Font** (from UsedFonts)  
        B. **Choose an Action Mode:**:
                    3-1. Fix & Clean — Replace fonts and remove unused text boxes  
                    3-2. Fix Only — Replace fonts only
        4. Ask the user if there is a path they want to save.
        - If the user did not answer the desired path: Save it as the default path if the file was opened immediately without any problems in No. 1, and specify the outputDirectory as null if you moved the file in the path you suggested.
        ** If the returned path starts with '/files/', the file is stored within the mounted path. If the user has responded to the desired path, this requires a file system tool to copy it from the mounted folder to the file path between the local and the container. If not, it should be notified that it is stored in the mounted folder. (Exists inside workspace)
        5. Report
        Provides the download link or file path that the Tool returns.
        """;
    }
}