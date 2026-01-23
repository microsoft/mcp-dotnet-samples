namespace McpSamples.PptFontFix.HybridApp.Models;

/// <summary>
/// This represents the result of Ppt font analysis.
/// </summary>

public class PptFontAnalyzeResult
{
    /// <summary>
    /// Gets or sets the list of used fonts in the presentation.
    /// </summary>
    public List<string> UsedFonts { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of unused fonts in the presentation.
    /// </summary>
    public List<string> UnusedFonts { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of inconsistently used fonts in the presentation.
    /// </summary>
    public List<string> InconsistentlyUsedFonts { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of inconsistently used font locations in the presentation.
    /// </summary>
    public List<FontUsageLocation> InconsistentFontLocations { get; set; } = new List<FontUsageLocation>();

    /// <summary>
    /// Gets or sets the list of unused font locations in the presentation.
    /// </summary>
    public List<FontUsageLocation> UnusedFontLocations { get; set; } = new List<FontUsageLocation>();
}

/// <summary>
/// This represents the location where a font is used in the presentation.
/// </summary>
public class FontUsageLocation
{
    /// <summary>
    /// Gets or sets the slide number where the font is used.
    /// </summary>
    public int SlideNumber { get; set; }

    /// <summary>
    /// Gets or sets the shape name where the font is used.
    /// </summary>
    public string ShapeName { get; set; } = string.Empty;
}