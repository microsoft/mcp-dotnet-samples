using ShapeCrawler;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Linq;
using McpSamples.PptFontFix.HybridApp.Models;

namespace McpSamples.PptFontFix.HybridApp.Services;

/// <summary>
/// This provides interface for PPT font fixing operations.
/// </summary>
public interface IPPTFontFixService
{
    /// <summary>
    /// open a PPT file.
    /// </summary>
    /// <param name="filePath"></param>
    Task OpenPPTFileAsync(string filePath);

    /// <summary>
    /// Analyze fonts in a PPT file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>A classified list of fonts used in the presentation.</returns>
    Task<PPTFontAnalyzeResult> AnalyzeFontsAsync();
}

/// <summary>
/// This represents the service entity for PPT font fixing.
/// </summary>
/// <param name="logger"></param>
public class PPTFontFixService(ILogger<PPTFontFixService> logger) : IPPTFontFixService
{
    private Presentation? _presentation;
    /// <inheritdoc />
    public async Task OpenPPTFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
        if (File.Exists(filePath) == false)
        {
            throw new FileNotFoundException("PPT file does not exist.", filePath);
        }

        try
        {
            this._presentation = new Presentation(filePath);
            logger.LogInformation("PPT file opened successfully and verified by ShapeCrawler: {FilePath}", filePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PPT file with ShapeCrawler: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PPTFontAnalyzeResult> AnalyzeFontsAsync()
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("PPT file is not opened. Please open a PPT file before analyzing fonts.");
        }

        var totalFontsInSlides = new HashSet<string>();
        var visibleFontUsages = new Dictionary<string, List<FontUsageLocation>>();
        var result = new PPTFontAnalyzeResult();
        var slideWidth = this._presentation.SlideWidth;
        var slideHeight = this._presentation.SlideHeight;

        Action<IParagraphPortion, bool, bool, FontUsageLocation> processPortion = (portion, isShapeVisible, isSlideVisible, location) =>
        {
            if (portion.Font == null) return;
            string? fontName = portion.Font.LatinName;
            if (string.IsNullOrEmpty(fontName)) return;

            totalFontsInSlides.Add(fontName);

            if (isSlideVisible && isShapeVisible)
            {
                bool isWhitespace = string.IsNullOrWhiteSpace(portion.Text);
                if (!isWhitespace)
                {
                    // Count only visible text portions
                    if (!visibleFontUsages.ContainsKey(fontName))
                    {
                        visibleFontUsages[fontName] = new List<FontUsageLocation>();
                    }
                    visibleFontUsages[fontName].Add(location);
                }
            }
        };

        foreach (var slide in this._presentation.Slides)
        {
            bool isSlideVisible = !slide.Hidden(); 
            foreach (var shape in slide.Shapes)
            {
                var location = new FontUsageLocation { SlideNumber = slide.Number, ShapeName = shape.Name };

                bool isEmptyBox = shape.TextBox != null && string.IsNullOrWhiteSpace(shape.TextBox.Text);
                if (isEmptyBox)
                {
                    logger.LogWarning("[Empty Box Detected] Slide Number: {SlideNumber}, Shape Name: {ShapeName}",
                        slide.Number, shape.Name);
                    result.UnusedFontLocations.Add(location);
                }

                bool isShapeVisible = !((shape.X + shape.Width) <= 0 ||
                                        shape.X >= slideWidth ||
                                        (shape.Y + shape.Height) <= 0 ||
                                        shape.Y >= slideHeight);

                if (!isShapeVisible && shape.TextBox != null && !isEmptyBox)
                {
                    logger.LogWarning("[Text Shape Outside Slide] Slide Number: {SlideNumber}, Shape Name: {ShapeName}",
                        slide.Number, shape.Name);
                    result.UnusedFontLocations.Add(location);
                }

                if (shape.TextBox != null)
                {
                    foreach (var portion in shape.TextBox.Paragraphs.SelectMany(p => p.Portions))
                    {
                        processPortion(portion, isShapeVisible, isSlideVisible, location);
                    }
                }
            }
        }

        var allVisibleFontNames = new HashSet<string>(visibleFontUsages.Keys);
        var unusedFonts = new HashSet<string>(totalFontsInSlides);
        unusedFonts.ExceptWith(allVisibleFontNames);
        result.UnusedFonts = unusedFonts.ToList();
        int standardFontCount = 2;
        var sortedVisibleFonts = visibleFontUsages
            .OrderByDescending(pair => pair.Value.Count)
            .ToList();
        result.UsedFonts = sortedVisibleFonts
            .Take(standardFontCount)
            .Select(pair => pair.Key)
            .ToList();
        var inconsistentPairs = sortedVisibleFonts
            .Skip(standardFontCount)
            .ToList();
        result.InconsistentlyUsedFonts = inconsistentPairs
            .Select(pair => pair.Key)
            .ToList();
        result.InconsistentFontLocations = inconsistentPairs
            .SelectMany(pair => pair.Value)
            .ToList();

        logger.LogInformation("[Result] Used (Standard) Fonts: {Fonts}", string.Join(", ", result.UsedFonts));
        logger.LogInformation("[Result] Unused Fonts: {Fonts}", string.Join(", ", result.UnusedFonts));
        logger.LogInformation("[Result] Inconsistently Used Fonts: {Fonts}", string.Join(", ", result.InconsistentlyUsedFonts));

        return await Task.FromResult(result);
    }
}