namespace McpSamples.AwesomeAzd.HybridApp.Models;

/// <summary>
/// This represents a template from the awesome-azd API.
/// </summary>
public class AwesomeAzdTemplate
{
    /// <summary>
    /// Gets or sets the unique identifier for the awesome-azd template.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title for the awesome-azd template.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the description for the awesome-azd template.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the awesome-azd template.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source URL of the awesome-azd template.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags associated with the awesome-azd template.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
