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
        Please search all the chatmodes, instructions, prompts, agents, and collections that are related to the search keyword, `{keyword}`.

        Here's the process to follow:

        1. Use the `awesome-copilot` MCP server.
        1. Search all chatmodes, instructions, prompts, and agents for the keyword provided.
        1. DO NOT load any chatmodes, instructions, prompts, or agents from the MCP server until the user asks to do so.
        1. Scan local chatmodes, instructions, prompts, and agents markdown files in `.github/chatmodes`, `.github/instructions`, `.github/prompts`, and `.github/agents` directories respectively.
        1. Compare existing chatmodes, instructions, prompts, and agents with the search results.
        1. Provide a structured response in a table format that includes the already exists, mode (chatmodes, instructions, prompts or agents), filename, title and description of each item found. 
           Here's an example of the table format:

           | Exists | Mode         | Filename               | Title         | Description   |
           |--------|--------------|------------------------|---------------|---------------|
           | ✅    | chatmodes    | chatmode1.md         | ChatMode 1    | Description 1 |
           | ❌    | instructions | instruction1.md      | Instruction 1 | Description 1 |
           | ✅    | prompts      | prompt1.md           | Prompt 1      | Description 1 |
           | ❌    | agents       | agent1.md            | Agent 1       | Description 1 |

           ✅ indicates that the item already exists in this repository, while ❌ indicates that it does not.

        1. If any item doesn't exist in the repository, ask which item the user wants to save.
        1. If the user wants to save it, save the item in the appropriate directory (`.github/chatmodes`, `.github/instructions`, `.github/prompts`, or `.github/agents`) 
           using the mode and filename, with NO modification.
        1. Include a search for Collections, which are made up of multiple chatmodes, instructions, prompts, and agents, but contain a name, description and tags.
        1. If there are any that match, provide a summary of the collection, including its name, description, tags, and the items it contains.
        1. Do NOT automatically install or save any items. Wait for explicit user confirmation.
        1. Use the table from above to show the items in the collection.
        """;
    }

    /// <summary>
    /// Gets a prompt to display details about a specific collection.
    /// </summary>
    [McpServerPrompt(Name = "search_collections", Title = "Prompt to search Collections in the awesome copilot repo by keyword")]
    [Description("Prompt to search Collections in the awesome copilot repo by keyword.")]
    public string SearchCollectionsPrompt([
        Description("The keyword to search the Collections for")]
        string keyword)
    {
        return $"""
        Please fetch the collection that matches `{keyword}` in the ID, name, description, or tags from the awesome-copilot MCP server collections endpoint.

        Provide a human-friendly summary that includes:
        - Collection name and description
        - Tags
        - A breakdown of items grouped by kind (chat-mode, instruction, prompt, agent) with filenames

        Do NOT automatically install or save any items. Wait for explicit user confirmation.
        """;
    }
}
