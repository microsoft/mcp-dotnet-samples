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
using Azure.Storage.Blobs;           // ⭐ Blob 추가
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace McpSamples.PptTranslator.HybridApp.Services;

public interface ITextExtractService
{
    Task OpenPptFileAsync(string filePath);
    Task<PptTextExtractResult> TextExtractAsync();
    Task<string> ExtractToJsonAsync(PptTextExtractResult extracted, string outputPath);
}

public class TextExtractService : ITextExtractService
{
    private readonly ILogger<TextExtractService> _logger;
    private readonly BlobServiceClient? _blobServiceClient;   // ⭐ Blob Client
    private Presentation? _presentation;

    public TextExtractService(
        ILogger<TextExtractService> logger,
        IConfiguration config)  // ⭐ Blob을 위해 IConfiguration 추가
    {
        _logger = logger;

        string? conn = config["AzureBlobConnectionString"];
        if (!string.IsNullOrWhiteSpace(conn))
            _blobServiceClient = new BlobServiceClient(conn);
    }

    public async Task OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        // ================================
        // 1) Blob URL로 들어온 경우 처리
        // ================================
        if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Blob URL을 처리할 수 없습니다. AzureBlobConnectionString이 없습니다.");

            _logger.LogInformation("Blob URL 감지됨 → 다운로드 시작: {Url}", filePath);

            try
            {
                // URL에서 container + blobname 추출
                string container = uri.Segments[1].Trim('/');
                string blobName = string.Join("", uri.Segments[2..]);

                var containerClient = _blobServiceClient.GetBlobContainerClient(container);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                    throw new FileNotFoundException($"Blob 파일을 찾을 수 없습니다: {container}/{blobName}");

                // ⭐ Blob에서 다운로드 후 메모리로 Presentation 생성
                var ms = new MemoryStream();
                await blobClient.DownloadToAsync(ms);
                ms.Position = 0;

                _presentation = new Presentation(ms);

                _logger.LogInformation("Blob PPT 로드 완료.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blob PPT 파일 로드 실패");
                throw;
            }
        }

        // ================================
        // 2) 기존 로컬 파일 처리 (변경 없음)
        // ================================
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
