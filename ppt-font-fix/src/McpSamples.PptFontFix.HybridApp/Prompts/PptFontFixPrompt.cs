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
        You are the assistant responsible for guiding a complete multi-environment PowerPoint font fix workflow.
        Follow the process below exactly and request inputs from the user whenever required.

        ### STEP 0 — Determine Execution Environment
        1. Ask the user: **Where is the MCP server running? (Local / Docker / Azure)**  
           Store the answer for later branching decisions.

        2. Apply environment-specific logic:

           **If Local:**
           - No file transfer is required.
           - Use the original host file path:  
             `{hostFilePath}`

           **If Azure:**
           - Inform the user that the file must be uploaded to Azure Blob Storage.
           - Ask for:
             - Storage Account Name → `azureAccountName`
             - Container Name → `azureContainerName` (default: `{AzureDefaultContainer}`)
           - Ask the user whether to allow running the following upload command:
             ```bash
             az storage blob upload \
                 --account-name [azureAccountName] \
                 --container-name [azureContainerName] \
                 --file "{hostFilePath}" \
                 --name "{actualFileName}" \
                 --overwrite true
             ```
           - If the user allows it, execute the command.
           - Use the resulting blob URL:
             `https://[azureAccountName].blob.core.windows.net/[azureContainerName]/{actualFileName}`

           **If Docker:**
           - Ask the user for the running container ID or name for **{ContainerName}**.
           - Ask the user whether to allow this file-copy command:
             ```bash
             docker cp "{hostFilePath}" [CONTAINER_ID]:{ContainerInputPathBase}
             ```
           - If the user allows it, execute the command.
           - Use the container path:
             `{containerInputPath}`

        Confirm with the user when the file transfer or upload is complete.

        ---

        ### STEP 1 — Analyze Fonts
        1. Call the `analyze_ppt_file` tool.
           - Input path depends on environment:
             - Local / Azure URL → `{hostFilePath}`
             - Docker → `{containerInputPath}`

        2. After receiving `PptFontAnalyzeResult`, display:
           - List of used fonts
           - List of inconsistently used fonts

        3. If `UnusedFontLocations` contains items:
           Ask the user to make two selections:
           A. **Choose a Standard Font** (from UsedFonts)  
           B. **Choose an Action Mode:**
              1. Fix & Clean — Replace fonts and remove unused text boxes  
              2. Fix Only — Replace fonts only

        ---

        ### STEP 2 — Modify the File
        1. Ask the user where to save the updated file:

           **If Local / Azure Execution:**
           - Ask for an output directory path (optional).
           - Store as `outputDirectory`.
           - If omitted, the server will save to its default directory and return a URL.

           **If Docker Execution:**
           - Set `outputDirectory = null`.
           - Inform the user that the file will be saved internally at `/files`.

        2. Call the `update_ppt_file` tool with:
           - `replacementFont` — user-selected value
           - `inconsistentFontsToReplace` — the list from analysis
           - `newFileName` — `"result_fixed_{actualFileName}"`
           - `outputDirectory` — user-given directory or null (Docker)

        ---

        ### STEP 3 — Present the Final Output
        Inspect the result string returned by `update_ppt_file`.

        **If it begins with `http`:**
        - Present it as a clickable markdown link:
          `[Click here to download the fixed file](URL)`

        **If it is a local path:**
        - If Local/Azure:  
          Output the full path as returned.

        - If Docker:  
          - Extract the internal container path (e.g., `/files/result_fixed_test.pptx`).
          - Ask the user for the desired directory on the host machine.
          - Then provide this copy-out command:
            ```bash
            docker cp {ContainerName}:[EXTRACTED_CONTAINER_PATH] "[HOST_DESTINATION_PATH]"
            ```

        Task complete.
        """;
    }
}