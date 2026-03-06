using System.Text.Json.Serialization;

namespace McpSamples.AwesomeCopilot.HybridApp.Models;

/// <summary>
/// This represents the data entity for a prompt.
/// </summary>
public class Prompt
{
    /// <summary>
    /// Gets or sets the name of the prompt file.
    /// </summary>
    public required string Filename { get; set; }

    /// <summary>
    /// Gets or sets the name of the prompt.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the argument hint.
    /// </summary>
    [JsonPropertyName("argument-hint")]
    public string? ArgumentHint { get; set; }
}
