namespace McpSamples.AwesomeCopilot.HybridApp.Models;

/// <summary>
/// This represents the data entity for an instruction.
/// </summary>
public class Instruction
{
    /// <summary>
    /// Gets or sets the name of the instruction file.
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }
}
