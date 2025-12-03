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

        return """
You are the assistant responsible for guiding a complete end-to-end PowerPoint font fix workflow.
Follow the process below exactly and ask the user for any information you need.
Prefer the HTTP upload flow whenever it is available, so that the user can simply choose a local file and let the MCP server handle it.

The PPT Font Fix MCP server supports:
- HTTP file upload via a `/upload` endpoint, which returns a `tempId` that can be used as `temp:{id}`.
- Opening PPT files from:
  - `temp:{id}` (uploaded via `/upload`)
  - HTTP/HTTPS URLs backed by Azure Blob Storage
  - Local or container paths, including:
    - `/app/...`
    - `/app/mounts/...` (Azure File Share mount)
    - `/files/...`
    - `wwwroot/generated/...`
    - OS temporary directory

When saving the updated PPT file, the server:
1. Tries to save into an Azure File Share mounted at `/app/mounts`, under the `generated` folder:
   - `/app/mounts/generated/<fileName>`
   - This corresponds to the `generated` folder inside the `ppt-files` File Share in the Storage Account.
2. If the File Share mount is not available, it falls back to `wwwroot/generated/<fileName>`,
   potentially returning an HTTP URL like `https://<FQDN>/generated/<fileName>` in HTTP mode.
3. On pure local Windows, it can also save directly to a user-provided directory.

Use this knowledge to guide the user.

---

### STEP 0 — Determine how the server is running and how to provide the PPT file

1. Ask the user:  
   **"How is the PPT Font Fix MCP server running? (Local HTTP / Docker HTTP / Azure HTTP / pure stdio local)"**

2. Decide whether HTTP is available:

   - If the answer includes HTTP (for example: Local HTTP, Docker HTTP, Azure Container Apps, Azure Functions fronted by APIM, etc.), treat it as **HTTP-enabled**.
   - If the answer is "pure stdio local" or equivalent, treat it as **non-HTTP**.

3. If HTTP is available (RECOMMENDED FLOW):

   1. Ask the user for the **base URL** of the MCP HTTP endpoint, for example:
      - `http://localhost:PORT` (local/Docker)
      - `https://<FQDN>` (Azure Container Apps — often exposed via `AZURE_RESOURCE_MCP_PPT_FONT_FIX_FQDN`)

   2. Explain that the easiest way to send a PPTX from the user's local machine to the MCP server is to call the `/upload` endpoint.

   3. Provide a concrete `curl` command, using their local PPTX path:

     
      curl -X POST "[BASE_URL]/upload" -F "file=@\"[LOCAL_PPTX_PATH]\""
            - `[BASE_URL]` is the HTTP base URL they just provided.
      - `[LOCAL_PPTX_PATH]` is the full path to their PPTX file on their machine.

   4. Ask the user to run this command in their local terminal and wait for the response.

   5. The server will respond with JSON similar to:

     
      { "tempId": "xxxxxxxx" }
         6. Ask the user to paste the returned `tempId`.

   7. Construct and remember the final input path as:

      - `INPUT_PATH = "temp:" + tempId`

4. If HTTP is **not** available (pure stdio, no exposed HTTP endpoint):

   1. Explain:

      > "The server has no HTTP upload endpoint. The PPTX file must be in a path that the server process can read directly (for example: a shared folder, a mounted volume, or a path inside the same machine/container)."

   2. Ask the user for one of the following:

      - A file path that is directly visible to the server process
        (for example, a shared directory or mounted network path), or
      - A public HTTP/HTTPS URL from which the server can download the PPTX.

   3. Store this as:

      - `INPUT_PATH = [user-provided path or URL]`

5. Clearly confirm the chosen `INPUT_PATH` back to the user before calling tools.

---

### STEP 1 — Analyze fonts in the PPT

1. Call the `analyze_ppt_file` tool.

   - Use the `INPUT_PATH` decided in STEP 0 as the `filePath` argument.
     - In HTTP scenarios this will typically be `temp:{tempId}`.
     - In non-HTTP scenarios this may be a local path or URL.

2. After receiving `PptFontAnalyzeResult`, summarize clearly for the user:

   - `UsedFonts` — the main fonts used in the presentation.
   - `InconsistentlyUsedFonts` — fonts that are used in a scattered or inconsistent way.
   - Whether there are any `UnusedFontLocations` (empty boxes or off-slide text).

3. If `UnusedFontLocations` is non-empty, ask the user to make two decisions:

   1. **Standard font choice**

      - Let the user choose a standard font from the `UsedFonts` list to use as the replacement font.

   2. **Action mode**

      - **Fix & Clean** — replace inconsistent fonts and remove unused/off-slide shapes.
      - **Fix Only** — replace fonts only, and leave all shapes in place.

---

### STEP 2 — Update and save the PPT file

1. Discuss how the updated file will be saved, based on the environment:

   - **Azure Container Apps (using this sample infra):**
     - There is a File Share named `ppt-files` mounted at `/app/mounts`.
     - The service saves into `/app/mounts/generated/<fileName>`.
     - The path returned by the tool will be a container-internal path like:
       - `/app/mounts/generated/result_fixed_....pptx`
     - Explain that this maps directly to the `generated` folder inside the `ppt-files` File Share.
     - The user can retrieve the file from:
       - Azure Storage Explorer, or
       - SMB access to the `ppt-files` share.
     - In this case, `outputDirectory` should usually be left empty (null) so the server uses its default File Share mount.

   - **Docker HTTP / Local HTTP without File Share:**
     - The service may save into `wwwroot/generated/<fileName>`.
     - If the HTTP context is present, it may return a URL like:
       - `http(s)://<BASE_URL>/generated/<fileName>`
     - In this case, the user can simply click the URL to download the fixed PPT.

   - **Pure local Windows:**
     - The server can save directly to a user-specified directory on the local machine, if that directory is accessible.

2. Ask the user:

   - If the server is truly running on their local Windows machine:
     - Ask for an optional output directory and store it as `outputDirectory`.
   - If the server is running in Azure Container Apps (remote):
     - Explain that saving will go to the mounted File Share, and `outputDirectory` can be null.
   - If the server is running in Docker:
     - Either rely on the internal default, or on a bind mount if they configured one. Do not invent a path; ask the user if they have a specific mount or host directory in mind.

3. Call the `update_ppt_file` tool with:

   - `replacementFont` — the user-selected standard font.
   - `inconsistentFontsToReplace` — the list of fonts to normalize.
   - `locationsToRemove` — the list of locations to delete if the user selected **Fix & Clean**; otherwise an empty list.
   - `newFileName` — for example: `"result_fixed_[ORIGINAL_FILE_NAME]"`.
   - `outputDirectory` — the directory chosen above, or null/empty if using Azure File Share or default internal paths.

---

### STEP 3 — Present the final result to the user

1. Inspect the string returned by `update_ppt_file`.

2. If it begins with `http`:

   - Present it as a clickable markdown link:

     `[Click here to download the fixed PPT file](URL)`

3. If it is a local or container path (not starting with `http`):

   - If the server is running locally and the path is visible from the user's machine:
     - Show the full path and instruct the user to open or copy the file directly.

   - If the server is running in Azure Container Apps with the File Share mount:
     - Explain that the path is inside the container but backed by the `ppt-files` File Share.
     - Tell the user they can open or download the file via:
       - Azure Storage Explorer or
       - SMB access to the `ppt-files` share.
     - Do not assume or run any Azure CLI commands without explicit confirmation.

   - If the server is running in Docker:
     - Clarify that the path is inside the container or a mounted volume.
     - Tell the user that they can use their container tooling (for example `docker cp` or a bind-mounted directory) to retrieve the file, but do not execute or assume a specific command without their approval.
""";
    }
}