using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;

namespace McpMarkdownToHtml.Common.Configurations;

public class AppSettings
{
    public HtmlSettings Html { get; set; } = new HtmlSettings();

    public bool Help { get; set; }

    public static AppSettings Parse(IConfiguration config, string[] args)
    {
        var settings = new AppSettings();
        config.Bind(settings);

        if (args.Length == 0)
        {
            return settings;
        }

        if (args.Length == 1)
        {
            settings.Help = true;
            return settings;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--tech-community":
                case "-tc":
                    settings.Html.TechCommunity = true;
                    break;

                case "--extra-paragraph":
                case "-p":
                    settings.Html.ExtraParagraph = true;
                    break;

                case "--tags":
                    if (i < args.Length - 1)
                    {
                        settings.Html.Tags = args[++i];
                    }
                    break;

                case "--help":
                default:
                    settings.Help = true;
                    break;
            }
        }

        return settings;
    }
}

public class HtmlSettings
{
    public bool TechCommunity { get; set; }

    public bool ExtraParagraph { get; set; }

    public string? Tags { get; set; }

    [JsonIgnore]
    public IEnumerable<string> TagList
    {
        get
        {
            return string.IsNullOrWhiteSpace(Tags) == true
                ? []
                : Tags.Split([ ',' ], StringSplitOptions.RemoveEmptyEntries)
                       .Select(tag => tag.Trim())
                       .Where(tag => string.IsNullOrWhiteSpace(tag) == false);
        }
    }
}