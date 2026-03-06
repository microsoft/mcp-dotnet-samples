using System.Text.Json.Serialization;

namespace McpSamples.AwesomeCopilot.HybridApp.Models;

/// <summary>
/// This represents the data entity for a hook.
/// </summary>
public class Hook
{
    /// <summary>
    /// Gets or sets the name of the hook file.
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// Gets or sets the name of the hook.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }
}
