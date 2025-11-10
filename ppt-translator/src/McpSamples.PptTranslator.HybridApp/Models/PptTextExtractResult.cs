namespace McpSamples.PptTranslator.HybridApp.Models
{
    /// <summary>
    /// Represents the overall text extraction result from a PPT file.
    /// </summary>
    public class PptTextExtractResult
    {
        public int TotalCount { get; set; }
        public List<PptTextExtractItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Represents a single extracted text element from a slide shape.
    /// </summary>
    public class PptTextExtractItem
    {
        public int SlideIndex { get; set; }
        public string ShapeId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
