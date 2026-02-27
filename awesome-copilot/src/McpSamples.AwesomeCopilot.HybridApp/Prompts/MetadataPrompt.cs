using System.ComponentModel;

using ModelContextProtocol.Server;

namespace McpSamples.AwesomeCopilot.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for metadata prompts.
/// </summary>
public interface IMetadataPrompt
{
    /// <summary>
    /// Gets a prompt for searching the awesome copilot content.
    /// </summary>
    /// <param name="keyword">The keyword to search for.</param>
    /// <returns>A formatted search prompt.</returns>
    string GetSearchPrompt(string keyword);
}

/// <summary>
/// This represents the prompts entity for the awesome-copilot repository.
/// </summary>
[McpServerPromptType]
public class MetadataPrompt : IMetadataPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "get_search_prompt", Title = "Prompt for searching the awesome copilot content")]
    [Description("Get a prompt for searching the awesome copilot content.")]
    public string GetSearchPrompt(
        [Description("The keyword to search for")] string keyword)
    {
        return $"""
        Please search all the agents, hooks, instructions, prompts, skills, and workflows that are related to the search keyword, `{keyword}`.

        Here's the process to follow:

        1. Use the `awesome-copilot` MCP server.
        1. Search all agents, hooks, instructions, prompts, skills, and workflows for the keyword provided.
        1. DO NOT load any items from the MCP server until the user asks to do so.
        1. Scan local instructions, prompts, and agents markdown files in `.github/instructions`, `.github/prompts`, and `.github/agents` directories respectively.
        1. Compare existing items with the search results.
        1. Provide a structured response in a table format that includes the already exists, mode (agents, hooks, instructions, prompts, skills, or workflows), filename, name and description of each item found.
           Here's an example of the table format:

           | Exists | Mode         | Filename                      | Name          | Description   |
           |--------|--------------|-------------------------------|---------------|---------------|
           | ✅    | agents       | agent1.agent.md               | Agent 1       | Description 1 |
           | ❌    | instructions | instruction1.instructions.md  | Instruction 1 | Description 1 |
           | ✅    | prompts      | prompt1.prompt.md             | Prompt 1      | Description 1 |
           | ❌    | skills       | skill1/SKILL.md               | Skill 1       | Description 1 |

           ✅ indicates that the item already exists in this repository, while ❌ indicates that it does not.

        1. If any item doesn't exist in the repository, ask which item the user wants to save.
        1. If the user wants to save it, save the item in the appropriate directory (`.github/instructions`, `.github/prompts`, `.github/agents`, etc.)
           using the mode and filename, with NO modification.
        1. Do NOT automatically install or save any items. Wait for explicit user confirmation.
        1. Use the table from above to show the items.
        """;
    }
}
