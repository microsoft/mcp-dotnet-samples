namespace McpSamples.AwesomeCopilot.HybridApp.Models;

/// <summary>
/// This represents the data entity for metadata.json.
/// </summary>
public class Metadata
{
    /// <summary>
    /// Gets or sets the list of <see cref="Agent"/> objects.
    /// </summary>
    public List<Agent> Agents { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of <see cref="Hook"/> objects.
    /// </summary>
    public List<Hook> Hooks { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of <see cref="Instruction"/> objects.
    /// </summary>
    public List<Instruction> Instructions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of <see cref="Prompt"/> objects.
    /// </summary>
    public List<Prompt> Prompts { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of <see cref="Skill"/> objects.
    /// </summary>
    public List<Skill> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of <see cref="Workflow"/> objects.
    /// </summary>
    public List<Workflow> Workflows { get; set; } = [];
}
