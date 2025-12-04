using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ShapeCrawler;
using ShapeCrawler.Presentations;
using Microsoft.Extensions.Configuration;
using McpSamples.PptTranslator.HybridApp.Models;

namespace McpSamples.PptTranslator.HybridApp.Services;

/// <summary>
/// Service for extracting text content from PowerPoint files.
/// </summary>
public interface ITextExtractService
{
    /// <summary>
    /// Opens a PowerPoint file for text extraction.
    /// </summary>
    /// <param name="filePath">Path to the PPT file</param>
    Task OpenPptFileAsync(string filePath);
    
    /// <summary>
    /// Extracts all text content from the opened presentation.
    /// </summary>
    /// <returns>Structured extraction result containing slide and shape text</returns>
    Task<PptTextExtractResult> TextExtractAsync();
    
    /// <summary>
    /// Serializes extracted text to JSON format.
    /// </summary>
    /// <param name="extracted">Extracted text data</param>
    /// <param name="outputPath">Output directory for JSON file</param>
    /// <returns>Path to the generated JSON file</returns>
    Task<string> ExtractToJsonAsync(PptTextExtractResult extracted, string outputPath);
}

/// <summary>
/// Default implementation of text extraction service using ShapeCrawler library.
/// Handles both local and Azure environments with appropriate path resolution.
/// </summary>
/// <remarks>
/// ShapeCrawler 라이브러리를 사용한 텍스트 추출 서비스 기본 구현.
/// 로컬 및 Azure 환경에서 적절한 경로 해석을 처리합니다.
/// </remarks>
public class TextExtractService : ITextExtractService
{
    private readonly ILogger<TextExtractService> _logger;
    private readonly bool _isAzure =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));

    private const string AzureInputMount = "/mnt/storage/input";
    private const string AzureOutputMount = "/mnt/storage/output";

    private Presentation? _presentation;

    public TextExtractService(
        ILogger<TextExtractService> logger,
        IConfiguration config)
    {
        _logger = logger;
    }

    public async Task OpenPptFileAsync(string filePath)
    {
        string resolved = filePath;

        // Azure 환경: temp 디렉토리 사용 금지, mount 경로 직접 사용
        if (_isAzure)
        {
            string fileName = Path.GetFileName(filePath);
            resolved = Path.Combine(AzureInputMount, fileName);

            if (!File.Exists(resolved))
                throw new FileNotFoundException("Azure PPT not found in mount folder.", resolved);

            _presentation = new Presentation(resolved);
            _logger.LogInformation("[Azure] PPT opened from mount: {Path}", resolved);
            return;
        }

        // 로컬 환경 (STDIO/HTTP/Container): 기존 로직 유지
        resolved = TempFileResolver.ResolveToTemp(filePath);

        if (!File.Exists(resolved))
            throw new FileNotFoundException("Resolved PPT file not found.", resolved);

        try
        {
            _presentation = new Presentation(resolved);
            _logger.LogInformation("[Local] PPT opened: {Resolved}", resolved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open PPT file.");
            throw;
        }
    }

    public Task<PptTextExtractResult> TextExtractAsync()
    {
        if (_presentation == null)
            throw new InvalidOperationException("PPT must be opened before extraction.");

        var result = new PptTextExtractResult();
        var items = new List<PptTextExtractItem>();

        foreach (var slide in _presentation.Slides)
        {
            foreach (var shape in slide.Shapes)
            {
                if (shape.TextBox == null)
                    continue;

                string text = shape.TextBox.Text?.Trim() ?? "";
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

        _logger.LogInformation("Extracted {Count} items", result.TotalCount);
        return Task.FromResult(result);
    }

    public async Task<string> ExtractToJsonAsync(PptTextExtractResult extracted, string outputPath)
    {
        // ==================================================
        // Azure 환경 → 무조건 mount output 에 저장
        // ==================================================
        if (_isAzure)
        {
            Directory.CreateDirectory(AzureOutputMount);

            string fileName = Path.GetFileName(outputPath);
            string savePath = Path.Combine(AzureOutputMount, fileName);

            var json = JsonSerializer.Serialize(extracted, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(savePath, json);

            _logger.LogInformation("[Azure] JSON extracted → {Path}", savePath);
            return savePath;
        }

        // ==================================================
        // Local 환경 → 기존 로직 유지
        // ==================================================
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var localJson = JsonSerializer.Serialize(extracted, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await File.WriteAllTextAsync(outputPath, localJson);
        return outputPath;
    }
}
