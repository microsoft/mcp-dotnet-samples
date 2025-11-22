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

        Example input:

        https://github.com/Azure-Samples/azure-search-openai-demo-csharp ; C:\projects\agent ; AzureEnv

        ------------------------------------------------------------
        3. Backend execution
        ------------------------------------------------------------
        - Call the execute_template tool with a JSON object like this:
        
            "srcPath": "Selected Template Source",
            "workingDirectory": <user input or null>,
            "envName": <user input or null>"
        


        - If the user leaves **Working Directory** or **Environment Name** empty,
          pass **null** for those values. The tool will handle defaults internally.

        - The tool will generate and execute the appropriate AZD command and return
          success status, output, and any errors.
        """;
    }
}
