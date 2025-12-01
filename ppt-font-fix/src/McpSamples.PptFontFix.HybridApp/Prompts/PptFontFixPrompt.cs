using System.ComponentModel;
using ModelContextProtocol.Server;
using System.IO; 
using System;
using System.Collections.Generic;
using System.Linq;

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
           - Inform the user: "**The server cannot directly access your local file path in the Azure environment. You must upload the file to Azure Blob Storage or a File Share and provide the accessible path.**"
           - Ask the user: **"Have you already uploaded the file to Azure storage? (Yes/No)"**
           - Store the answer as `isUploaded`.

           - **IF `isUploaded` is "No":**
             - Inform the user: "**You must run the following command in your terminal to upload the file to Blob Storage.**"
             - Ask for:
               - Storage Account Name → `azureAccountName`
               - Account Key → `azureAccountKey`
               - Container Name → `azureContainerName` (default: `{AzureDefaultContainer}`)
             - Present the upload command and ask for permission to run it:
               ```bash
               az storage blob upload \
                   --account-name [azureAccountName] \
                   --account-key [azureAccountKey] \
                   --container-name [azureContainerName] \
                   --file "{hostFilePath}" \
                   --name "{actualFileName}" \
                   --overwrite true
               ```
             - If the user allows it, execute the command.
             - **Wait for confirmation** that the upload has executed successfully.

           - **FINAL CRITICAL ACTION (Execute regardless of `isUploaded`):** Request the final access path for the server to read the file.
           - Ask the user: "**If the file has been uploaded, please provide one of the following:** 1) **Blob URL (including SAS)**, 2) **Blob Path (e.g., `[azureContainerName]/{actualFileName}`)** or 3) **File Share File Name (e.g., `{actualFileName}`)**"
           - Store the final input path for the tool as `FINAL_INPUT_PATH = "[User-Provided Path/URL/File Name]"`

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

           **If Local Execution:**
           - Ask for an output directory path (optional).
           - Store as `outputDirectory`.
           - If omitted, the server will save to its default directory and return a URL.

           **If Azure Execution:**
           - Inform the user: "**The modified file will be saved to Blob Storage and provided as a secure download URL.**" 
           - Set `outputDirectory = null`.

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
        """;
    }
}
