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
    /// The keys "AuthorUrl" and "website" have the same meaning but are spelled differently in the JSON file.
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
    /// The keys "azure_service" and "AzureService" have the same meaning but are spelled differently in the JSON file.
    /// </summary>
    public List<string> AzureServices { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of main programming languages used by the awesome-azd template.
    /// The keys "language" and "languages" have the same meaning but are spelled differently in the JSON file.
    /// </summary>
    public List<string> Languages { get; set; } = new();
}


public class CommandExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
}