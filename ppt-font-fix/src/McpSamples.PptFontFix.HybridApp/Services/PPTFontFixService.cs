using ShapeCrawler;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Linq;
using McpSamples.PptFontFix.HybridApp.Models;

namespace McpSamples.PptFontFix.HybridApp.Services;

/// <summary>
/// This provides interface for Ppt font fixing operations.
/// </summary>
public interface IPptFontFixService
{
    /// <summary>
    /// open a Ppt file.
    /// </summary>
    /// <param name="filePath"></param>
    Task OpenPptFileAsync(string filePath);

    /// <summary>
    /// Analyze fonts in a Ppt file.
    /// </summary>
    /// <returns>A classified list of fonts used in the presentation.</returns>
    Task<PptFontAnalyzeResult> AnalyzeFontsAsync();

    /// <summary>
    /// Save the modified Ppt file.
    /// </summary>
    /// <param name="newFilePath"></param>
    Task SavePptFileAsync(string newFilePath);

    /// <summary>
    /// Remove Unused Fonts from the presentation.
    /// </summary>
    /// <param name="locationsToRemove">A list of shape locations to be removed.</param>
    Task<int> RemoveUnusedFontsAsync(List<FontUsageLocation> locationsToRemove);

    ///<summary>
    /// Replace a font with another font throughout the presentation.
    /// </summary>
    /// <param name="fontToReplace">The font name to be replaced.</param>
    /// <param name="replacementFont">The replacement font name.</param>
    /// <returns>The number of replacements made.</returns>
    Task<int> ReplaceFontAsync(string fontToReplace, string replacementFont);
}

/// <summary>
/// This represents the service entity for Ppt font fixing.
/// </summary>
/// <param name="logger"></param>
public class PptFontFixService(ILogger<PptFontFixService> logger) : IPptFontFixService
{
    private Presentation? _presentation;

    private HashSet<string>? _analyzedVisibleFonts;
    /// <inheritdoc />
    public async Task OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
        if (File.Exists(filePath) == false)
        {
            throw new FileNotFoundException("Ppt file does not exist.", filePath);
        }

        try
        {
            this._presentation = new Presentation(filePath);

            this._analyzedVisibleFonts = null;
            logger.LogInformation("Ppt file opened successfully and verified by ShapeCrawler: {FilePath}", filePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open Ppt file with ShapeCrawler: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PptFontAnalyzeResult> AnalyzeFontsAsync()
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before analyzing fonts.");
        }

        var totalFontsInSlides = new HashSet<string>();
        var visibleFontUsages = new Dictionary<string, List<FontUsageLocation>>();
        var result = new PptFontAnalyzeResult();
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
                    if (!visibleFontUsages.TryGetValue(fontName, out List<FontUsageLocation>? value))
                    {
                        value = [];
                        visibleFontUsages[fontName] = value;
                    }

                    value.Add(location);
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
        
        this._analyzedVisibleFonts = new HashSet<string>(allVisibleFontNames, StringComparer.OrdinalIgnoreCase);
        
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

    /// <inheritdoc />
    public async Task SavePptFileAsync(string newFilePath)
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before saving.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(newFilePath, nameof(newFilePath));

        try
        {
            this._presentation.Save(newFilePath);
            logger.LogInformation("Ppt file saved successfully: {FilePath}", newFilePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save Ppt file: {FilePath}", newFilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveUnusedFontsAsync(List<FontUsageLocation> locationsToRemove)
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before removing unused fonts.");
        }

        if (locationsToRemove == null || locationsToRemove.Count == 0)
        {
            logger.LogInformation("No locations provided for removal. Skipping removal process.");
            return await Task.FromResult(0);
        }

        int removalCount = 0;

        foreach (var location in locationsToRemove)
        {
            var slide = this._presentation.Slides.FirstOrDefault(s => s.Number == location.SlideNumber);
            if (slide == null)
            {
                logger.LogWarning("Slide number {SlideNumber} not found. Skipping.", location.SlideNumber);
                continue;
            }

            var shape = slide.Shapes.FirstOrDefault(sh => sh.Name == location.ShapeName);
            if (shape == null)
            {
                logger.LogWarning("Shape name {ShapeName} not found in slide {SlideNumber}. Skipping.", location.ShapeName, location.SlideNumber);
                continue;
            }

            shape.Remove();
            removalCount++;
            logger.LogInformation("Removed shape {ShapeName} from slide {SlideNumber}.", location.ShapeName, location.SlideNumber);
        }

        logger.LogInformation("Total shapes removed: {Count}", removalCount);
        return await Task.FromResult(removalCount);
    }
    
    /// <inheritdoc />
    public async Task<int> ReplaceFontAsync(string fontToReplace, string replacementFont)
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before replacing fonts.");
        }

        if (string.IsNullOrWhiteSpace(fontToReplace))
        {
            throw new ArgumentException("Font to replace cannot be null or whitespace.", nameof(fontToReplace));
        }

        if (string.IsNullOrWhiteSpace(replacementFont))
        {
            throw new ArgumentException("Replacement font cannot be null or whitespace.", nameof(replacementFont));
        }

        if (!this._analyzedVisibleFonts.Contains(replacementFont))
        {
            throw new ArgumentException($"The font '{replacementFont}' is not a valid font found in the presentation's visible text. Please choose from the analyzed list.", nameof(replacementFont));
        }


        int replacementCount = 0;

        foreach (var slide in this._presentation.Slides)
        {
            foreach (var shape in slide.Shapes)
            {
                if (shape.TextBox != null)
                {
                    foreach (var portion in shape.TextBox.Paragraphs.SelectMany(p => p.Portions))
                    {
                        if (portion.Font != null && string.Equals(portion.Font.LatinName, fontToReplace, StringComparison.OrdinalIgnoreCase))
                        {
                            portion.Font.LatinName = replacementFont;
                            replacementCount++;
                        }
                    }
                }
            }
        }

        logger.LogInformation("Total font replacements made: {Count}", replacementCount);
        return await Task.FromResult(replacementCount);
    }
}