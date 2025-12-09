using System.ComponentModel;
using ModelContextProtocol.Server;
using System.Linq;

namespace McpSamples.PptTranslator.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for PPT Translator prompts.
/// </summary>
public interface IPptTranslatorPrompt
{
    /// <summary>
    /// Gets a prompt to start the PPT translation workflow.
    /// </summary>
    /// <param name="hostFilePath">The full path to the PPTX file on the host machine or a public URL.</param>
    /// <param name="targetLang">The target language code.</param>
    /// <param name="downloadPath">The optional download path for the translated file.</param>
    string GetTranslationPrompt(string hostFilePath, string targetLang, string? downloadPath = null);
}

/// <summary>
/// This represents the prompts entity for the PptTranslator system.
/// </summary>
[McpServerPromptType]
public class PptTranslatorPrompt : IPptTranslatorPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "translate_ppt", Title = "Start PPT Translation Workflow")]
    [Description("Generate a structured workflow prompt for analyzing and translating PPT files.")]
    public string GetTranslationPrompt(
        [Description("The full path to the PPTX file or a public URL.")] string hostFilePath,
        [Description("The target language code (e.g., 'ko', 'en', 'ja')")] string targetLang,
        [Description("The optional download path for the translated file. Press Enter to use default locations.")] string? downloadPath = null)
    {
        string downloadPathInfo = string.IsNullOrWhiteSpace(downloadPath) 
            ? "If not specified (press Enter to use default):"
            : $"Specified save path: `{downloadPath}`";

        return $"""
                The user wants to translate the PPT file located at **{hostFilePath}** to **{targetLang}**.

                ### 🎯 Translation Request

                * **Source File:** `{hostFilePath}`
                * **Target Language:** `{targetLang}`
                * **Save Path:** {downloadPathInfo}

                ### 🚀 How to Execute

                Simply call the **translate_ppt_file** tool with the following parameters:
                - **filePath:** `{hostFilePath}`
                - **targetLang:** `{targetLang}`{(string.IsNullOrWhiteSpace(downloadPath) ? "" : $"\n                - **outputPath:** `{downloadPath}`")}

                The tool will automatically handle:
                ✅ File access and validation
                ✅ Content extraction 
                ✅ Translation to target language
                ✅ File generation and saving
                ✅ Result delivery

                ### 🚧 Automated Error Handling Instructions

                **If the tool returns a file access error:**

                1. **For Container Volume Mount Errors:**
                   - **Automatically execute** the appropriate copy command based on the user's operating system:
                   - **macOS/Linux:** Run `cp "{hostFilePath}" [VOLUME_MOUNT_PATH]` in terminal
                   - **Windows CMD:** Run `copy "{hostFilePath}" [VOLUME_MOUNT_PATH]` in terminal
                   - **Windows PowerShell:** Run `Copy-Item "{hostFilePath}" -Destination [VOLUME_MOUNT_PATH]` in terminal
                   - Replace `[VOLUME_MOUNT_PATH]` with the actual path from the error message
                   - **Then retry** the `translate_ppt_file` tool with the corrected file path

                2. **For URL Upload Scenarios:**
                   - **Automatically execute** the upload command based on the user's operating system:
                   - **macOS/Linux:** Run `curl -F "file=@{hostFilePath}" [UPLOAD_URL]` in terminal
                   - **Windows:** Run `curl.exe -F "file=@{hostFilePath}" [UPLOAD_URL]` in terminal
                   - Replace `[UPLOAD_URL]` with the actual URL from the error message
                   - **Then retry** the `translate_ppt_file` tool using the uploaded file reference

                **Important:** You must automatically execute these commands using the terminal tool, not instruct the user to copy and paste them manually.

                ### 📍 Expected Output Location

                {(string.IsNullOrWhiteSpace(downloadPath) ? 
                    "- **Local Environment:** `wwwroot/generated/`\n                - **Container Environment:** Mount path (e.g., `/files/`)\n                - **Azure Environment:** Download link will be provided" :
                    $"- **Specified Path:** `{downloadPath}`")}
                """;
    }
}