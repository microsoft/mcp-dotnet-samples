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
    private const string ContainerName = "ppt-font-fix";
    private const string ContainerInputPathBase = "/files";
    private const string AzureDefaultContainer = "generated-files";
    private const string AzureFileShareName = "ppt-files";
    
    /// <inheritdoc />
    [McpServerPrompt(Name = "fix_ppt_fonts", Title = "Start PPT Font Fix Workflow")]
    [Description("Generate a structured workflow prompt for analyzing and fixing PPT fonts.")]
    public string GetAnalysisPrompt(
        [Description("The full path to the PPTX file or a public URL.")] string hostFilePath)
    {
        string safePath = hostFilePath.Replace('\\', '/');
        string actualFileName = safePath.Split('/').Last();
        string containerInputPath = $"{ContainerInputPathBase}/{actualFileName}";

        return $"""
        The user wants to fix font issues in the PPT file located at **{hostFilePath}** using the **MCP-based PPT Font Fix Workflow**.
        
        ### 🎯 Workflow Objective
        
        1.  Start by using the **Open PPT File** tool to assess the environment (local vs. container access).
        2.  If file access fails, guide the user with the necessary **file transfer/copy command** based on the error message.
        3.  Once the file is ready, proceed to analyze and fix fonts using the **Analyze PPT File** tool.
        4.  Clearly instruct the user on how to access the final resulting file.

        ---

        ### 🛠️ Step 1: Open PPT File (Initial Assessment)

        You must first call the `Open PPT File` tool with `{hostFilePath}`. The goal is to determine if the file is directly accessible (e.g., local host or public URL) or if a container environment requires a transfer.
        
        * **If local access is successful:** Proceed immediately to Step 2 (Analyze PPT File).
        * **If file access fails:** Use the error message returned by the tool to guide the user:

        ### 🚧 Step 1.1: Handling File Access Failure

        1.  **If the message "COPY the file from your local machine path..." is returned (Volume Mounting Scenario):**
            * You must suggest the following **local copy command** to the user:
                `copy {hostFilePath} [VOLUME_MOUNT_PATH]`
        
        2.  **If the message "Please execute the appropriate command for your operating system (OS)..." is returned (URL Upload Scenario):**
            * You must suggest the appropriate **OS-specific cURL upload command** to the user, using the `uploadUrl` provided in the message:
            
            ```
            ### 🍎/🐧 macOS & Linux (Example)
            
              curl -F "file=@{hostFilePath}" uploadUrl
            
            ### 💻 Windows (PowerShell/CMD Example)
            
              curl.exe -F "file=@{hostFilePath}" uploadUrl
            ```
            
            * **Upon successful upload:** You must be prepared to call the **`Analyze PPT File` tool** and pass the **`uploadUrl` as the `url` parameter** in the next step.
        
        ### ⚙️ Step 2: Analyze PPT File (Analysis and Fix)
        
        When calling the `Analyze PPT File` tool, handle the **save path** (`savePath`) based on user input:
        
        * **If the user did not specify a save path:** Leave the `savePath` **empty**. (If this causes an error in the tool, you may use `hostFilePath` as a temporary default path.)
        * **If the user specified a desired save path:** Use that path as the `savePath`.

        ### ✅ Step 3: Result Handling and Guidance

        Based on the output returned by the `Analyze PPT File` tool, guide the user on how to access the result:
        
        * **If the return value starts with `http`:** Present it to the user as a **clickable link**.
        * **If the return value is a local/container path:** Instruct the user on the **specific method** required to access the file (e.g., checking a file share, accessing a specific mounted volume).
        """;
    }
}