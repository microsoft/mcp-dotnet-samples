using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShapeCrawler;
using ShapeCrawler.Presentations;
using Azure.Storage.Blobs;                // ⭐ Blob 추가
using Azure.Storage.Sas;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public interface IFileRebuildService
    {
        Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang);
    }

    public class FileRebuildService : IFileRebuildService
    {
        private readonly ILogger<FileRebuildService> _logger;
        private readonly BlobServiceClient? _blobServiceClient;   // ⭐ BlobClient 지원
        private readonly IConfiguration _config;

        public FileRebuildService(
            ILogger<FileRebuildService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            string? conn = config["AzureBlobConnectionString"];
            if (!string.IsNullOrWhiteSpace(conn))
                _blobServiceClient = new BlobServiceClient(conn);
        }

        public async Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang)
        {
            // ============================================================================================
            // ① Blob URL 로딩 지원 (팀원 방식 그대로)
            // ============================================================================================
            Stream originalStream;

            if (IsBlobUrl(pptFilePath))
            {
                _logger.LogInformation("[Blob] PPT 다운로드 시작: {Url}", pptFilePath);

                if (_blobServiceClient == null)
                    throw new InvalidOperationException("Blob URL을 사용할 수 없습니다. AzureBlobConnectionString이 없습니다.");

                originalStream = await DownloadBlobIntoStream(pptFilePath);
            }
            else
            {
                if (!File.Exists(pptFilePath))
                    throw new FileNotFoundException("Original PPT file not found.", pptFilePath);

                originalStream = File.OpenRead(pptFilePath);
            }

            // translated JSON 확인
            if (!File.Exists(translatedJsonPath))
                throw new FileNotFoundException("Translated JSON file not found.", translatedJsonPath);

            // JSON 읽기
            string jsonContent = await File.ReadAllTextAsync(translatedJsonPath);
            var translated = JsonSerializer.Deserialize<TranslatedResult>(jsonContent)
                ?? throw new Exception("Failed to parse translated JSON.");

            ValidateTranslatedJson(translated);

            // ============================================================================================
            // ② 메모리 스트림에 복사하여 프레젠테이션 객체 생성
            // ============================================================================================
            var workingStream = new MemoryStream();
            await originalStream.CopyToAsync(workingStream);
            workingStream.Position = 0;

            var pres = new Presentation(workingStream);

            // 적용
            foreach (var item in translated.Items)
            {
                try
                {
                    var slide = pres.Slides[item.SlideIndex - 1];
                    var shape = FindShapeById(slide, item.ShapeId);

                    if (shape?.TextBox != null)
                        ApplyTranslatedText(shape.TextBox, item.Text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to apply translated text. Slide={Slide}, ShapeId={ShapeId}",
                        item.SlideIndex, item.ShapeId);
                }
            }

            // ============================================================================================
            // ③ 저장 파일 이름 설정
            // ============================================================================================
            string finalFileName = $"{Path.GetFileNameWithoutExtension(pptFilePath)}_translated_{targetLang}.pptx";

            // Azure 사용 여부
            string? connStr = _config["AzureBlobConnectionString"];

            // ============================================================================================
            // ④ Azure Blob Storage로 저장
            // ============================================================================================
            if (!string.IsNullOrWhiteSpace(connStr) && _blobServiceClient != null)
            {
                try
                {
                    _logger.LogInformation("[Azure] 번역 PPT Blob 저장 시작...");

                    var container = _blobServiceClient.GetBlobContainerClient("generated-files");
                    await container.CreateIfNotExistsAsync();

                    var blob = container.GetBlobClient(finalFileName);

                    var uploadStream = new MemoryStream();
                    pres.Save(uploadStream);
                    uploadStream.Position = 0;

                    await blob.UploadAsync(uploadStream, overwrite: true);

                    var sasBuilder = new BlobSasBuilder()
                    {
                        BlobContainerName = "generated-files",
                        BlobName = finalFileName,
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);

                    Uri sasUri = blob.GenerateSasUri(sasBuilder);

                    _logger.LogInformation("[Azure] 업로드 완료 → {Url}", sasUri);

                    return sasUri.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Azure] 업로드 실패 → 로컬 저장으로 fallback");
                }
            }

            // ============================================================================================
            // ⑤ 로컬 저장 fallback
            // ============================================================================================
            string localPath = Path.Combine(Path.GetTempPath(), finalFileName);

            using (var fs = new FileStream(localPath, FileMode.Create))
            {
                var saveStream = new MemoryStream();
                pres.Save(saveStream);
                saveStream.Position = 0;
                await saveStream.CopyToAsync(fs);
            }

            _logger.LogInformation("로컬 저장 완료: {Path}", localPath);

            return localPath;
        }

        // ==========================================================
        // Blob 도우미 함수들 (팀원 코드 구조 그대로)
        // ==========================================================
        private bool IsBlobUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private async Task<Stream> DownloadBlobIntoStream(string blobUrl)
        {
            var uri = new Uri(blobUrl);

            string containerName = uri.Segments[1].Trim('/');
            string blobName = string.Join("", uri.Segments[2..]);

            var container = _blobServiceClient!.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobName);

            if (!await blob.ExistsAsync())
                throw new FileNotFoundException($"Blob not found: {containerName}/{blobName}");

            var ms = new MemoryStream();
            await blob.DownloadToAsync(ms);
            ms.Position = 0;

            return ms;
        }

        // ============================================================================
        // 기존 코드 (구조 그대로 유지)
        // ============================================================================
        private void ApplyTranslatedText(ITextBox textBox, string translatedText)
        {
            var paragraphs = textBox.Paragraphs;
            if (paragraphs.Count == 0)
                paragraphs.Add();

            var firstParagraph = paragraphs[0];
            ITextPortionFont? baseFont =
                firstParagraph.Portions.Count > 0 ? firstParagraph.Portions[0].Font : null;

            firstParagraph.Text = string.Empty;

            var lines = translatedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            firstParagraph.Portions.AddText(lines[0]);
            ApplyStyleIfAvailable(firstParagraph, baseFont);

            for (int i = 1; i < paragraphs.Count; i++)
                paragraphs[i].Text = string.Empty;

            for (int i = 1; i < lines.Length; i++)
            {
                IParagraph p;
                if (i < paragraphs.Count)
                    p = paragraphs[i];
                else
                {
                    paragraphs.Add();
                    p = paragraphs[i];
                }

                p.Text = lines[i];
                ApplyStyleIfAvailable(p, baseFont);
            }
        }

        private void ApplyStyleIfAvailable(IParagraph p, ITextPortionFont? baseFont)
        {
            if (baseFont == null || p.Portions.Count == 0) return;

            var f = p.Portions.Last().Font;
            if (f == null) return;

            f.Size = baseFont.Size;
            f.IsBold = baseFont.IsBold;
            f.IsItalic = baseFont.IsItalic;
            f.Underline = baseFont.Underline;
            f.Color.Set(baseFont.Color.Hex);
            f.LatinName = baseFont.LatinName;
            f.EastAsianName = baseFont.EastAsianName;
        }

        private void ValidateTranslatedJson(TranslatedResult json)
        {
            if (json.Items == null)
                throw new Exception("Translated JSON has no items.");

            if (json.TotalCount != json.Items.Count)
                throw new Exception("JSON TotalCount does not match Items count.");

            foreach (var i in json.Items)
            {
                if (i.SlideIndex <= 0)
                    throw new Exception($"Invalid SlideIndex: {i.SlideIndex}");
                if (string.IsNullOrWhiteSpace(i.ShapeId))
                    throw new Exception("ShapeId cannot be empty.");
                if (!int.TryParse(i.ShapeId, out _))
                    throw new Exception($"ShapeId must be numeric: {i.ShapeId}");
            }
        }

        private IShape? FindShapeById(ISlide slide, string id)
        {
            foreach (var s in slide.Shapes)
                if (s.Id.ToString() == id)
                    return s;

            return null;
        }

        private class TranslatedResult
        {
            public int TotalCount { get; set; }
            public List<TranslatedItem> Items { get; set; } = new();
        }

        private class TranslatedItem
        {
            public int SlideIndex { get; set; }
            public string ShapeId { get; set; } = "";
            public string Text { get; set; } = "";
        }
    }
}
