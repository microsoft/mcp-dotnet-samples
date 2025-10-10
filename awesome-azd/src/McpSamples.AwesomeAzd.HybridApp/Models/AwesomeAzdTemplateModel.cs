namespace McpSamples.AwesomeAzd.HybridApp.Models;

/// <summary>
/// This represents a template from the awesome-azd API.
/// </summary>
public class AwesomeAzdTemplateModel
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
    /// Gets or sets the preview image path for the awesome-azd template.
    /// </summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author URL (e.g., GitHub profile or team page).
    /// </summary>
    public string AuthorUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author name of the awesome-azd template.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source repository URL of the awesome-azd template.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags that describe the awesome-azd template.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of Azure services used by the awesome-azd template.
    /// </summary>
    public List<string> AzureServices { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of main programming languages used by the awesome-azd template.
    /// </summary>
    public List<string> Languages { get; set; } = new();
}
