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

        1. Use the `awesome-azd` MCP server.
        2. Search all templates in the **Awesome AZD repository** using the `get_templates` tool.
        3. Return the results in the following **table format**:

          | Title | Description | Source |
          |-------|-------------|--------|
          | ...   | ...         | ...    |

        4. After showing the table, ask the user to **select one template by its Title**.

        5. Once the user selects a Title:
          - Retrieve the selected template's `source` value.
          - Convert the `source` URL into the proper `<path>` for AZD:

            Rules:
            - If the URL starts with:
              `https://github.com/Azure-Samples/...`
              → Use `Azure-Samples/<repo>`.

            - Otherwise, extract the normal `owner/repo`.

          - Build the AZD command:
            ```
            azd init -t <path> --non-interactive --no-prompt
            ```

            **IMPORTANT**  
            MCP executes commands in a non-TTY environment.  
            Without `--non-interactive` and `--no-prompt`, `azd init` will wait for input and **never finish**.  
            So always include those flags.

        6. **Immediately execute this command using the MCP tool `ExecuteTemplateCommandAsync`:**

            ```
            ExecuteTemplateCommandAsync("<generated command>", "<working directory>")
            ```

            - The working directory is where the template project should be initialized.

        7. Return the execution result (Success, Output, Error) back to the user.

        """;
    }

}