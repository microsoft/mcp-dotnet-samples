namespace McpSamples.AwesomeCopilot.HybridApp.Models;

/// <summary>
/// This represents the data entity for a workflow.
/// </summary>
public class Workflow
{
    /// <summary>
    /// Gets or sets the name of the workflow file.
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }
}
