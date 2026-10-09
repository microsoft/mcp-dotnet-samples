namespace McpSamples.AwesomeAzd.HybridApp.Models;

/// <summary>
/// This represents a simplified model of an awesome-azd template for LLM responses.
/// </summary>
public class AwesomeAzdTemplateResponse
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
    /// Gets or sets the source repository URL of the awesome-azd template.
    /// </summary>
    public string Source { get; set; } = string.Empty;

}
