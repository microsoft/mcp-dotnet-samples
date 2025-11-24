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

/// <summary>
/// Represents the result of executing a terminal command.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command executed successfully.
    /// True if the command exited with code 0; otherwise, false.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the standard output produced by the executed command.
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// Gets or sets the standard error produced by the executed command.
    /// If an exception occurred or the command failed, this may contain the error message.
    /// </summary>
    public string Error { get; set; } = "";
}