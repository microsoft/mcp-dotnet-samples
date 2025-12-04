using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShapeCrawler;
using ShapeCrawler.Presentations;
using Microsoft.Extensions.Configuration;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public interface IFileRebuildService
    {
        Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang, string outputPath);
    }

    public class FileRebuildService : IFileRebuildService
    {
        private readonly ILogger<FileRebuildService> _logger;
        private readonly bool _isAzure;

        private static readonly string UploadRoot =
            Path.Combine(Path.GetTempPath(), "mcp-uploads");

        public FileRebuildService(
            ILogger<FileRebuildService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));

            if (!_isAzure && !Directory.Exists(UploadRoot))
                Directory.CreateDirectory(UploadRoot);
        }

        public async Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang, string outputPath)
        {
            _logger.LogInformation("[Rebuild] START - Original paths: PPT={0}, JSON={1}", pptFilePath, translatedJsonPath);
            
            string resolvedPptPath = ResolvePath(pptFilePath);
            _logger.LogInformation("[Rebuild] Resolved PPT path: {0}", resolvedPptPath);
            
            string resolvedJsonPath = ResolvePath(translatedJsonPath);
            _logger.LogInformation("[Rebuild] Resolved JSON path: {0}", resolvedJsonPath);

            if (!File.Exists(resolvedPptPath))
            {
                _logger.LogError("[Rebuild] PPT file NOT FOUND: {0}", resolvedPptPath);
                throw new FileNotFoundException("PPT file not found.", resolvedPptPath);
            }
            _logger.LogInformation("[Rebuild] PPT file EXISTS, size: {0} bytes", new FileInfo(resolvedPptPath).Length);

            if (!File.Exists(resolvedJsonPath))
            {
                _logger.LogError("[Rebuild] JSON file NOT FOUND: {0}", resolvedJsonPath);
                throw new FileNotFoundException("Translated JSON file not found.", resolvedJsonPath);
            }
            _logger.LogInformation("[Rebuild] JSON file EXISTS, size: {0} bytes", new FileInfo(resolvedJsonPath).Length);

            _logger.LogInformation("[Rebuild] Input PPT: {0}", resolvedPptPath);
            _logger.LogInformation("[Rebuild] Translated JSON: {0}", resolvedJsonPath);

            var originalStream = File.OpenRead(resolvedPptPath);
            string jsonContent = await File.ReadAllTextAsync(resolvedJsonPath);

            var translated = JsonSerializer.Deserialize<TranslatedResult>(jsonContent)
                ?? throw new Exception("Failed to parse translated JSON.");

            ValidateTranslatedJson(translated);

            _logger.LogInformation("[Rebuild] Creating working stream copy...");
            var workingStream = new MemoryStream();
            await originalStream.CopyToAsync(workingStream);
            workingStream.Position = 0;
            _logger.LogInformation("[Rebuild] Working stream size: {0} bytes", workingStream.Length);

            _logger.LogInformation("[Rebuild] Loading PPT with ShapeCrawler.Presentation...");
            Presentation pres;
            try
            {
                pres = new Presentation(workingStream);
                _logger.LogInformation("[Rebuild] ShapeCrawler Presentation loaded successfully! Slide count: {0}", pres.Slides.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Rebuild] FAILED to load Presentation with ShapeCrawler!");
                throw;
            }

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
                        "[Rebuild] Failed apply text: Slide={Slide}, ShapeId={ShapeId}",
                        item.SlideIndex, item.ShapeId);
                }
            }

            // 전달받은 outputPath 사용 (이미 디렉터리가 생성되어 있어야 함)
            _logger.LogInformation("[Rebuild] Saving to output path: {0}", outputPath);
            
            string outputDir = Path.GetDirectoryName(outputPath) ?? "";
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                _logger.LogInformation("[Rebuild] Creating output directory: {0}", outputDir);
                Directory.CreateDirectory(outputDir);
            }
            
            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                _logger.LogInformation("[Rebuild] Calling pres.Save()...");
                var ms = new MemoryStream();
                pres.Save(ms);
                _logger.LogInformation("[Rebuild] pres.Save() completed, stream size: {0} bytes", ms.Length);
                ms.Position = 0;
                await ms.CopyToAsync(fs);
                _logger.LogInformation("[Rebuild] File written to disk: {0} bytes", fs.Length);
            }

            _logger.LogInformation("[Rebuild] Output saved successfully: {0}", outputPath);
            return outputPath;
        }


        // ==========================================================
        // 경로 해석: Azure와 로컬 temp 방식을 분리
        // ==========================================================
        private string ResolvePath(string path)
        {
            if (_isAzure)
            {
                // Azure에서는 마운트된 경로 그대로 사용해야 한다.
                return path;
            }

            // 로컬에서는 기존 temp 시스템 유지
            Directory.CreateDirectory(UploadRoot);

            if (path.StartsWith("temp:", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(UploadRoot, path.Substring(5));

            if (File.Exists(path))
            {
                string id = Guid.NewGuid().ToString("N");
                string dest = Path.Combine(UploadRoot, id);
                File.Copy(path, dest, overwrite: true);
                return dest;
            }

            throw new InvalidOperationException($"Invalid path '{path}'.");
        }

        // ==========================================================
        // Azure → /output/{GUID}_translated_lang.pptx
        // Local → 기존 temp 폴더
        // ==========================================================
        private string GetOutputPath(string lang)
        {
            string fileName = $"{Guid.NewGuid()}_translated_{lang}.pptx";

            if (_isAzure)
            {
                // Azure File Share 마운트 경로
                return Path.Combine("/output", fileName);
            }

            return Path.Combine(Path.GetTempPath(), fileName);
        }


        private void ApplyTranslatedText(ITextBox textBox, string translatedText)
        {
            var paragraphs = textBox.Paragraphs;
            if (paragraphs.Count == 0)
                paragraphs.Add();

            var first = paragraphs[0];
            var baseFont = first.Portions.Count > 0 ? first.Portions[0].Font : null;

            first.Text = "";
            var lines = translatedText.Split('\n');

            first.Portions.AddText(lines[0]);
            ApplyStyle(first, baseFont);

            for (int i = 1; i < paragraphs.Count; i++)
                paragraphs[i].Text = "";

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
                ApplyStyle(p, baseFont);
            }
        }

        private void ApplyStyle(IParagraph p, ITextPortionFont? baseFont)
        {
            if (baseFont == null || p.Portions.Count == 0) return;

            var f = p.Portions[0].Font;
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
