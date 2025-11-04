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
        Please search all Azure templates that are related to the search keyword `{keyword}`.

        Here's the process to follow:

        1. Use the `awesome-azd` MCP server.

        1. Search all templates in the **Awesome AZD repository** for the given keyword with get_templates tool.

        1. Return a structured response in a **table format** that includes:
           - Title  
           - Description  

        1. Example table format:

           | Title            | Description                    |
           |------------------|--------------------------------|
           | Starter - Bicep  | A starter template with Bicep  |

        1. Once a template is selected, **return all available details** about the selected template, including:
           - `title`
           - `description`
           - `preview`
           - `author`
           - `authorUrl`
           - `source`
           - `tags`
           - `azureServices`
           - `languages`
           - `id`

        1. If the user wants to execute this template, provide a command guide using the following rule:
           - Use the syntax:  
             ``
             azd init -t <path>
             ```
           - `<path>` is determined as follows:
             - If the GitHub source URL starts with  
               `https://github.com/Azure-Samples/...`,  
               then use the organization/repository name:
               ```
               e.g. azd init -t Azure-Samples/azure-openai-chat-frontend
               ```
             - Otherwise, include the full `owner/repo` path:
               ```
               e.g. azd init -t pascalvanderheiden/ais-apim-openai
               ```
        """;
    }
}