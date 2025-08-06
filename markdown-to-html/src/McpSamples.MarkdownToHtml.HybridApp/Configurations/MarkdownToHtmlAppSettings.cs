using System.Text.Json.Serialization;

using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.MarkdownToHtml.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for markdown-to-html app.
/// </summary>
public class MarkdownToHtmlAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Markdown to HTML",
        Version = "1.0.0",
        Description = "A simple MCP server for converting markdown to HTML."
    };

    /// <summary>
    /// Gets or sets the <see cref="HtmlSettings"/> instance.
    /// </summary>
    public HtmlSettings Html { get; set; } = new HtmlSettings();

    /// <inheritdoc />
    protected override T ParseMore<T>(IConfiguration config, string[] args)
    {
        var settings = base.ParseMore<T>(config, args);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--tech-community":
                case "-tc":
                    (settings as MarkdownToHtmlAppSettings)!.Html.TechCommunity = true;
                    break;

                case "--extra-paragraph":
                case "-p":
                    (settings as MarkdownToHtmlAppSettings)!.Html.ExtraParagraph = true;
                    break;

                case "--tags":
                    if (i < args.Length - 1)
                    {
                        (settings as MarkdownToHtmlAppSettings)!.Html.Tags = args[++i];
                    }
                    break;

                default:
                    settings.Help = true;
                    break;
            }
        }

        return settings;
    }
}

/// <summary>
/// This represents the HTML settings for the markdown-to-html app.
/// </summary>
public class HtmlSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to use the Tech Community style.
    /// </summary>
    public bool TechCommunity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to add an extra paragraph.
    /// </summary>
    public bool ExtraParagraph { get; set; }

    /// <summary>
    /// Gets or sets a comma delimited value indicating the tags to apply.
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Gets the list of tags parsed from the Tags property.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<string> TagList
    {
        get
        {
            return string.IsNullOrWhiteSpace(Tags) == true
                ? []
                : Tags.Split([','], StringSplitOptions.RemoveEmptyEntries)
                       .Select(tag => tag.Trim())
                       .Where(tag => string.IsNullOrWhiteSpace(tag) == false);
        }
    }
}