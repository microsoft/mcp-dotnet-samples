using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShapeCrawler;
using ShapeCrawler.Presentations;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    /// <summary>
    /// Provides functionality for rebuilding a PPT file using translated JSON data.
    /// </summary>
    public interface IFileRebuildService
    {
        Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath);
    }

    /// <summary>
    /// Rebuilds a PPT file by applying translated text from JSON to the original presentation.
    /// </summary>
    public class FileRebuildService : IFileRebuildService
    {
        private readonly ILogger<FileRebuildService> _logger;

        public FileRebuildService(ILogger<FileRebuildService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath)
        {
            if (!File.Exists(pptFilePath))
                throw new FileNotFoundException("Original PPT file not found.", pptFilePath);

            if (!File.Exists(translatedJsonPath))
                throw new FileNotFoundException("Translated JSON file not found.", translatedJsonPath);

            _logger.LogInformation("Rebuild process started.");

            string jsonContent = await File.ReadAllTextAsync(translatedJsonPath);
            var translated = JsonSerializer.Deserialize<TranslatedResult>(jsonContent);

            if (translated == null || translated.Items == null)
                throw new Exception("Invalid translated JSON structure.");

            string outputPath = Path.Combine(
                Path.GetDirectoryName(pptFilePath)!,
                "translated_output.pptx");

            File.Copy(pptFilePath, outputPath, overwrite: true);

            var pres = new Presentation(outputPath);

            foreach (var item in translated.Items)
            {
                var slide = pres.Slides[item.SlideIndex - 1];

                foreach (var shape in slide.Shapes)
                {
                    if (shape.Id.ToString() != item.ShapeId)
                        continue;

                    var textBox = shape.TextBox;
                    if (textBox == null)
                        continue;

                    if (textBox.Paragraphs.Count == 0 ||
                        textBox.Paragraphs[0].Portions.Count == 0)
                        continue;

                    var portion = textBox.Paragraphs[0].Portions[0];

                    // 기존 스타일 백업
                    var originalSize = portion.Font.Size;
                    var originalBold = portion.Font.IsBold;
                    var originalItalic = portion.Font.IsItalic;
                    var originalUnderline = portion.Font.Underline;
                    var originalColorHex = portion.Font.Color.Hex;

                    // 텍스트 적용 (TranslatedText → Text)
                    portion.Text = item.Text;

                    // 원본 스타일 복원
                    portion.Font.Size = originalSize;
                    portion.Font.IsBold = originalBold;
                    portion.Font.IsItalic = originalItalic;
                    portion.Font.Underline = originalUnderline;
                    portion.Font.Color.Set(originalColorHex);
                }
            }

            pres.Save();
            _logger.LogInformation("Rebuild process completed.");

            return outputPath;
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
