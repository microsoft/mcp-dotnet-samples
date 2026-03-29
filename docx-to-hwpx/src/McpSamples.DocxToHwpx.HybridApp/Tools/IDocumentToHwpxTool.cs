namespace McpSamples.DocxToHwpx.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the document (.docx, .html or .md) to .hwpx conversion tool.
/// </summary>
public interface IDocumentToHwpxTool
{
    /// <summary>
    /// Converts a document (.docx, .html or .md) file to .hwpx file.
    /// </summary>
    /// <param name="input">The input filepath.</param>
    /// <param name="output">The output .hwpx filepath.</param>
    /// <param name="reference">The reference filepath.</param>
    /// <returns>The converted .hwpx document path.</returns>
    Task<string> ConvertAsync(string? input, string? output, string? reference = null);
}
