using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ShapeCrawler;
using ShapeCrawler.Presentations;
using McpSamples.PptTranslator.HybridApp.Models;

namespace McpSamples.PptTranslator.HybridApp.Services;

public interface ITextExtractService
{
    /// <summary>
    /// Opens a PPT file for text extraction.
    /// </summary>
    Task OpenPptFileAsync(string filePath);

    /// <summary>
    /// Extracts all text items from the opened PPT file.
    /// </summary>
    Task<PptTextExtractResult> TextExtractAsync();

    /// <summary>
    /// Saves extracted text into a JSON file.
    /// </summary>
    Task<string> ExtractToJsonAsync(PptTextExtractResult extracted, string outputPath);
}

/// <summary>
/// Extracts text content from PPT files.
/// </summary>
public class TextExtractService : ITextExtractService
{
    private readonly ILogger<TextExtractService> _logger;
    private Presentation? _presentation;

    public TextExtractService(ILogger<TextExtractService> logger)
    {
        _logger = logger;
    }

    public Task OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("PPT file not found.", filePath);

        try
        {
            _presentation = new Presentation(filePath);
            _logger.LogInformation("[INFO] PPT file opened.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR] Failed to open PPT file.");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<PptTextExtractResult> TextExtractAsync()
    {
        if (_presentation == null)
            throw new InvalidOperationException("PPT file must be opened before extraction.");

        var result = new PptTextExtractResult();
        var items = new List<PptTextExtractItem>();

        try
        {
            foreach (var slide in _presentation.Slides)
            {
                foreach (var shape in slide.Shapes)
                {
                    if (shape.TextBox == null)
                        continue;

                    string text = shape.TextBox.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    items.Add(new PptTextExtractItem
                    {
                        SlideIndex = slide.Number,
                        ShapeId = shape.Id.ToString(),
                        Text = text
                    });
                }
            }

            result.Items = items;
            result.TotalCount = items.Count;

            _logger.LogInformation("[INFO] Extracted {Count} items.", result.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR] Failed during text extraction.");
            throw;
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<string> ExtractToJsonAsync(PptTextExtractResult extracted, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(extracted);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        try
        {
            var json = JsonSerializer.Serialize(extracted, options);
            await File.WriteAllTextAsync(outputPath, json);

            _logger.LogInformation("[INFO] Extracted JSON saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR] Failed to save extracted JSON.");
            throw;
        }

        return Path.GetFullPath(outputPath);
    }
}
