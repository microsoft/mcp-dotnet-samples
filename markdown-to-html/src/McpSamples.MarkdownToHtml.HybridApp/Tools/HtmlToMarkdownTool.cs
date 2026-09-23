using System.ComponentModel;
using ReverseMarkdown;

using ModelContextProtocol.Server;

namespace McpSamples.MarkdownToHtml.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the HTML to markdown tool.
/// </summary>
public interface IHtmlToMarkdownTool
{
    /// <summary>
    /// Converts HTML text to markdown.
    /// </summary>
    /// <param name="html"></param>
    /// <returns></returns>
    Task<string> ConvertAsync(string html);
}

/// <summary>
/// This represents the tool entity for converting HTML to markdown.
/// </summary>
/// <param name="logger"></param>
[McpServerToolType]
public class HtmlToMarkdownTool(ILogger<HtmlToMarkdownTool> logger) : IHtmlToMarkdownTool
{
    [McpServerTool(Name = "convert_html_to_markdown", Title = "Convert HTML to Markdown")]
    [Description("Converts HTML text to Markdown.")]
    public async Task<string> ConvertAsync([Description("The HTML text")] string html)
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
