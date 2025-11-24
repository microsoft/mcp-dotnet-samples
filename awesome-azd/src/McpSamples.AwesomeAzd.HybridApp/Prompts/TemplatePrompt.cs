using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.AwesomeAzd.HybridApp.Prompts;

/// <summary>
/// Provides an interface for generating prompts that guide template searches.
/// </summary>
public interface ITemplatePrompt
{
    /// <summary>
    /// Gets a prompt for searching Azure templates by keyword.
    /// </summary>
    /// <param name="keyword">The keyword to search for.</param>
    /// <returns>A formatted search prompt.</returns>
    string GetSearchPrompt(string keyword);
}

/// <summary>
/// Represents the prompt entity for the Awesome AZD templates repository.
/// </summary>
[McpServerPromptType]
public class TemplatePrompt : ITemplatePrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "get_template_search_prompt", Title = "Prompt for searching AZD templates")]
    [Description("Get a prompt for searching Azure templates by keyword.")]
    public string GetSearchPrompt(
        [Description("The keyword to search for")] string keyword)
    {
        return $"""
        Please search all Azure templates related to the keyword `{keyword}`.

        ------------------------------------------------------------
        1. Search templates
        ------------------------------------------------------------
        - Use the `awesome-azd` MCP server.
        - Call the `get_templates` tool with the keyword `{keyword}`.
        - Return the results as a table with the columns: Title | Description | Source.
        - Only include templates whose metadata matches the keyword.

        ------------------------------------------------------------
        2. Collect user input for execution
        ------------------------------------------------------------
        After displaying the search results, ask the user to provide:

        1. **Selected Template Source** (must match the Source column; e.g., GitHub URL)
        2. **Working Directory** (optional; leave empty for default/null)
        3. **Environment Name** (optional; leave empty for default/null)

        - If the user provides a **Selected Template Source**, you may proceed to execution immediately.

        - If the user does NOT provide a working directory: Automatically determine the host machine's user profile directory by running a terminal command (Run in Terminal)

        - On Windows:
            echo %USERPROFILE%

        - On macOS or Linux:
            echo $HOME

        Use the returned value as `hostUserProfile`.

        Example input:

        https://github.com/Azure-Samples/azure-search-openai-demo-csharp ; C:\Users\<UserProfile>\Projects ; AzureEnv

        ------------------------------------------------------------
        3. Backend execution
        ------------------------------------------------------------
        - Call the `make_command` MCP tool with a JSON object like this:

            "srcPath": "Selected Template Source",
            "hostUserProfile": "<host user's profile path>",
            "workingDirectory": <user input or null>,
            "envName": <user input or null>

        - The tool will return an `AzdCommand` object.
        - Use `run in terminal` to execute it:
        
        1. `mkdir -p <AzdCommand.WorkingDirectory>` (Linux/macOS)  
        or `New-Item -ItemType Directory -Force -Path <AzdCommand.WorkingDirectory>` (Windows PowerShell)
        2. `cd <AzdCommand.WorkingDirectory>`
        3. Run `<AzdCommand.Command>`

        - Once you receive the AzdCommand object, immediately execute it in the terminal.
        - The command will initialize the template in the specified directory and return success status, output, and any errors.
        """;

    }
}
