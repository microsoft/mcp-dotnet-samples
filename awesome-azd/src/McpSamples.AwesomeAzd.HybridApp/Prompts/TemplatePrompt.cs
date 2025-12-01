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
        2. Collect user input for execution and Backend execution
        ------------------------------------------------------------
        After displaying the search results, ask the user to provide:

        1. **Selected Template Source** (must match the Source column; e.g., GitHub URL)
        2. **Working Directory** (optional; leave empty for default/null)
        3. **Environment Name** (optional; leave empty for default/null)

        - If the user provides a **Selected Template Source**, you may proceed to execution immediately.

        **IMPORTANT** - If the user does not provide a working directory or an environment name, keep asking repeatedly until they provide one.

        Example input:

        https://github.com/Azure-Samples/azure-search-openai-demo-csharp ; C:\Users\<UserProfile>\Projects ; AzureEnv


        - Call the `make_command` MCP tool with a JSON object like this:

            "srcPath": "Selected Template Source",
            "workingDirectory",
            "envName"

        - The tool will return an `AzdCommand` object.
        - Use `run in terminal` to execute it:
        
        - mkdir "<AzdCommand.WorkingDirectory>" -Force; cd "<AzdCommand.WorkingDirectory>"; <AzdCommand.Command> (Window)
        - mkdir -p "<AzdCommand.WorkingDirectory>" && cd "<AzdCommand.WorkingDirectory>" && <AzdCommand.Command> (MacOS, Linux)

        - Once the command is generated, execute it immediately without further confirmation.
        - The command will initialize the template in the specified directory and return success status, output, and any errors.
        """;

    }
}
