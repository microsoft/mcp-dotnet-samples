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
    private readonly ExecutionMode _executionMode;
    private Presentation? _presentation;

    public TextExtractService(
        ILogger<TextExtractService> logger,
        IConfiguration config)
    {
        _logger = logger;
        _executionMode = ExecutionModeDetector.DetectExecutionMode();
    }

    public Task OpenPptFileAsync(string filePath)
    {
        string resolved = filePath;

        if (_executionMode.IsContainerMode())
        {
            // Container/Azure 모드: 통합된 /files/input 사용
            string fileName = Path.GetFileName(filePath);
            resolved = Path.Combine("/files/input", fileName);

            if (!File.Exists(resolved))
                throw new FileNotFoundException("PPT file not found in /files/input folder.", resolved);

            _presentation = new Presentation(resolved);
            _logger.LogInformation("[Container] PPT opened from /files/input: {Path}", resolved);
            return Task.CompletedTask;
        }
        else
        {
            // 로컬 모드: 파일 복사
            if (File.Exists(filePath))
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "mcp-uploads");
                Directory.CreateDirectory(tempDir);
                string id = Guid.NewGuid().ToString("N");
                resolved = Path.Combine(tempDir, id);
                File.Copy(filePath, resolved, overwrite: true);
            }
            else
            {
                throw new FileNotFoundException("PPT file not found.", filePath);
            }

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
            
            return Task.CompletedTask;
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
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        var json = JsonSerializer.Serialize(extracted, jsonOptions);
        
        if (_executionMode.IsContainerMode())
        {
            // Container/Azure 모드: 통합된 /files/tmp 사용
            string fileName = Path.GetFileName(outputPath);
            string savePath = Path.Combine("/files/tmp", fileName);
            
            Directory.CreateDirectory("/files/tmp");
            await File.WriteAllTextAsync(savePath, json);
            
            _logger.LogInformation("[Container] JSON extracted → {Path}", savePath);
            return savePath;
        }
        else
        {
            // 로컬 모드: 기존 로직 유지
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(outputPath, json);
            _logger.LogInformation("[Local] JSON extracted → {Path}", outputPath);
            return outputPath;
        }
    }
}
