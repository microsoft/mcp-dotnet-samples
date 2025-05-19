namespace McpMarkdownToHtml.Common.Extensions;

/// <summary>
/// This represents the extension class for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Adds an empty paragraph element.
    /// </summary>
    /// <param name="html">HTML string value.</param>
    /// <param name="tag1">An HTML tag.</param>
    /// <param name="tag2">An HTML tag.</param>
    /// <returns>Returns the HTML string value with the empty paragraph added.</returns>
    public static string AddEmptyParagraph(this string html, string tag1, string tag2)
    {
        if (string.IsNullOrWhiteSpace(tag1) || string.IsNullOrWhiteSpace(tag2))
        {
            return html;
        }

        var updated = html.Replace($"</{tag1}>\n<{tag2}>", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2}>")
                          .Replace($"</{tag1}>\n\r<{tag2}>", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2}>")
                          .Replace($"</{tag1}>\r\n<{tag2}>", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2}>")

                          .Replace($"</{tag1}>\n<{tag2} ", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2} ")
                          .Replace($"</{tag1}>\n\r<{tag2} ", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2} ")
                          .Replace($"</{tag1}>\r\n<{tag2} ", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2} ")
                          ;

        return updated;
    }

    /// <summary>
    /// Adds an empty paragraph element.
    /// </summary>
    /// <param name="html">HTML string value.</param>
    /// <param name="tags1">List of HTML tags.</param>
    /// <param name="tag2">An HTML tag.</param>
    /// <returns>Returns the HTML string value with the empty paragraph added.</returns>
    public static string AddEmptyParagraph(this string html, IEnumerable<string> tags1, string tag2)
    {
        if (tags1.Any() == false || string.IsNullOrWhiteSpace(tag2))
        {
            return html;
        }

        var updated = html;
        foreach (var tag1 in tags1)
        {
            updated = updated.Replace($"</{tag1}>\n<{tag2}>", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2}>")
                             .Replace($"</{tag1}>\n\r<{tag2}>", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2}>")
                             .Replace($"</{tag1}>\r\n<{tag2}>", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2}>")

                             .Replace($"</{tag1}>\n<{tag2} ", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2} ")
                             .Replace($"</{tag1}>\n\r<{tag2} ", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2} ")
                             .Replace($"</{tag1}>\r\n<{tag2} ", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2} ")
                             ;
        }

        return updated;
    }

    /// <summary>
    /// Adds an empty paragraph element.
    /// </summary>
    /// <param name="html">HTML string value.</param>
    /// <param name="tag1">An HTML tag.</param>
    /// <param name="tags2">List of HTML tags.</param>
    /// <returns>Returns the HTML string value with the empty paragraph added.</returns>
    public static string AddEmptyParagraph(this string html, string tag1, IEnumerable<string> tags2)
    {
        if (string.IsNullOrWhiteSpace(tag1) || tags2.Any() == false)
        {
            return html;
        }

        var updated = html;
        foreach (var tag2 in tags2)
        {
            updated = updated.Replace($"</{tag1}>\n<{tag2}>", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2}>")
                             .Replace($"</{tag1}>\n\r<{tag2}>", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2}>")
                             .Replace($"</{tag1}>\r\n<{tag2}>", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2}>")

                             .Replace($"</{tag1}>\n<{tag2} ", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2} ")
                             .Replace($"</{tag1}>\n\r<{tag2} ", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2} ")
                             .Replace($"</{tag1}>\r\n<{tag2} ", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2} ")
                             ;
        }

        return updated;
    }

    /// <summary>
    /// Adds an empty paragraph element.
    /// </summary>
    /// <param name="html">HTML string value.</param>
    /// <param name="tags1">List of HTML tags.</param>
    /// <param name="tags2">List of HTML tags.</param>
    /// <returns>Returns the HTML string value with the empty paragraph added.</returns>
    public static string AddEmptyParagraph(this string html, IEnumerable<string> tags1, IEnumerable<string> tags2)
    {
        if (tags1.Any() == false || tags2.Any() == false)
        {
            return html;
        }

        var updated = html;
        foreach (var tag1 in tags1)
        {
            foreach (var tag2 in tags2)
            {
                updated = updated.Replace($"</{tag1}>\n<{tag2}>", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2}>")
                                 .Replace($"</{tag1}>\n\r<{tag2}>", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2}>")
                                 .Replace($"</{tag1}>\r\n<{tag2}>", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2}>")

                                 .Replace($"</{tag1}>\n<{tag2} ", $"</{tag1}>\n<p>&nbsp;</p>\n<{tag2} ")
                                 .Replace($"</{tag1}>\n\r<{tag2} ", $"</{tag1}>\n\r<p>&nbsp;</p>\n\r<{tag2} ")
                                 .Replace($"</{tag1}>\r\n<{tag2} ", $"</{tag1}>\r\n<p>&nbsp;</p>\r\n<{tag2} ")
                                 ;
            }
        }

        return updated;
    }
}
