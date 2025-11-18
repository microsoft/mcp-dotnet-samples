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

        Follow this process:

        ------------------------------------------------------------
        1. Search templates
        ------------------------------------------------------------
        - Use the `awesome-azd` MCP server.
        - Call the `get_templates` tool.
        - Filter all templates whose metadata contains the keyword `{keyword}`.

        ------------------------------------------------------------
        2. Return results as a table
        ------------------------------------------------------------
        Return ONLY this table format:

        | Title | Description | Source |
        |-------|-------------|---------|
        | ...   | ...         | ...     |

        After the table, ask the user for the following **three inputs**:

        - **Selected Title** (must match a Title from the table)
        - **Working Directory** (default = "<current_working_directory>/<template_name>")
        - **Environment Name** (default = "myenv")
        
        Ask the user to separate input values with semicolons (;): <Title> ; <WorkingDirectory or empty> ; <EnvironmentName or empty>
          ex) OpenAI Agent ; C:\projects\agent ; AzureEnv

        ------------------------------------------------------------
        3. After the user selects a template (Title)
        ------------------------------------------------------------
        - Find the template's `source` from the search results.
        - Convert `source` URL into AZD `<path>` using these rules:

            If source starts with:
            `https://github.com/Azure-Samples/<repo>`
             → AZD path = `Azure-Samples/<repo>`

            Otherwise:
             → Extract `owner/repo` from GitHub URL.

        ------------------------------------------------------------
        4.1 Build the AZD init command
        ------------------------------------------------------------
        Construct:

            azd init -t <path> --environment <envName>

        Notes:
        - If user did NOT provide Environment Name:
            Use default: `myenv`

        ------------------------------------------------------------
        4.2 Working directory rules
        ------------------------------------------------------------
        If the user provides a directory:
            Use that directory.

        If the user does NOT provide a directory:
            you MUST pass `null` as the working directory.
            (The backend will automatically apply the default behavior.)

        ------------------------------------------------------------
        5. Execute the command
        ------------------------------------------------------------
        Use:
            Call the `execute_command` tool.
            ExecuteTemplateCommandAsync(
                "<generated command>",
                "<chosen working directory OR null>"
            );

        After execution, return:
        - Success / Failure
        - Output
        - Error (if any)
        """;
    }
}
