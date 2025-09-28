using System.ComponentModel;
using System.Text.RegularExpressions;

using Markdig;
using ReverseMarkdown;

using McpSamples.MarkdownToHtml.HybridApp.Configurations;
using McpSamples.MarkdownToHtml.HybridApp.Extensions;

using ModelContextProtocol.Server;

namespace McpSamples.MarkdownToHtml.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the markdown to HTML tool.
/// </summary>
public interface IMarkdownToHtmlTool
{
    /// <summary>
    /// Converts markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The markdown text.</param>
    /// <returns>The converted HTML text.</returns>
    Task<string> ConvertAsync(string markdown);

    /// <summary>
    /// Converts HTML text to markdown.
    /// </summary>
    /// <param name="html"></param>
    /// <returns></returns>
    Task<string> ReverseConvertAsync(string html);
}

/// <summary>
/// This represents the tool entity for converting markdown to HTML.
/// </summary>
/// <param name="settings"><see cref="MarkdownToHtmlAppSettings"/> instance.</param>
/// <param name="regex"><see cref="Regex"/> instance for processing code blocks.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class MarkdownToHtmlTool(MarkdownToHtmlAppSettings settings, Regex regex, ILogger<MarkdownToHtmlTool> logger) : IMarkdownToHtmlTool
{
    private readonly HtmlSettings _settings = settings.Html ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
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

            if (_settings.TechCommunity == false)
            {
                return html;
            }

            if (_settings.TagList?.Any() == false)
            {
                return html;
            }

            html = regex.Replace(html, "<li-code lang=\"$1\">")
                        .Replace("</code></pre>", "</li-code>");
            if (_settings.ExtraParagraph == true)
            {
                html = html.AddEmptyParagraph(_settings.TagList!, _settings.TagList!);
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting markdown to HTML");

            html = $"<p>Error: {ex.Message}</p>";
        }

        return await Task.FromResult(html);
    }


    [McpServerTool(Name = "convert_html_to_markdown", Title = "Convert HTML to Markdown")]
    [Description("Converts HTML text to Markdown.")]
    public async Task<string> ReverseConvertAsync([Description("The HTML text")] string html)
    {
        var converter = new Converter();
        var markdown = default(string);

        try
        {
            markdown = converter.Convert(html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting HTML to Markdown");
            markdown = $"Error: {ex.Message}";
        }

        return await Task.FromResult(markdown);
    }
}
