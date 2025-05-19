using System.ComponentModel;
using System.Text.RegularExpressions;

using Markdig;

using McpMarkdownToHtml.Common.Configurations;
using McpMarkdownToHtml.Common.Extensions;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

namespace McpMarkdownToHtml.Common.Tools;

public interface IMarkdownToHtmlTool
{
    Task<string> ConvertAsync(string markdown);
}

[McpServerToolType]
public class MarkdownToHtmlTool(HtmlSettings settings, Regex regex, ILogger<MarkdownToHtmlTool> logger) : IMarkdownToHtmlTool
{
    [McpServerTool(Name = "convert_markdown_to_html", Title = "Convert Markdown to HTML")]
    [Description("Converts markdown text to HTML.")]
    public async Task<string> ConvertAsync([Description("The markdown text")] string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
                           .UseAdvancedExtensions()
                           .UseEmojiAndSmiley()
                           .UseYamlFrontMatter()
                           .Build();

        var html = default(string);
        try
        {
            html = Markdown.ToHtml(markdown, pipeline);

            if (settings.TechCommunity == false)
            {
                return html;
            }

            if (settings.TagList?.Any() == false)
            {
                return html;
            }

            html = regex.Replace(html, "<li-code lang=\"$1\">")
                         .Replace("</code></pre>", "</li-code>");
            if (settings.ExtraParagraph == true)
            {
                html = html.AddEmptyParagraph(settings.TagList!, settings.TagList!);
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting markdown to HTML");

            html = $"<p>Error: {ex.Message}</p>";
        }

        return await Task.FromResult(html);
    }
}
