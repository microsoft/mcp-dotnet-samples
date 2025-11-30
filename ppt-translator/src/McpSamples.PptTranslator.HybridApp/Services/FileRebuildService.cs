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
    /// Rebuilds PPTX files using translated JSON data.
    /// </summary>
    public interface IFileRebuildService
    {
        Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang);
    }

    /// <summary>
    /// Rebuilds a PPT file by inserting translated text into the appropriate shapes.
    /// </summary>
    public class FileRebuildService : IFileRebuildService
    {
        private readonly ILogger<FileRebuildService> _logger;

        public FileRebuildService(ILogger<FileRebuildService> logger)
        {
            _logger = logger;
        }

        public async Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath, string targetLang)
        {
            string originalName = Path.GetFileNameWithoutExtension(pptFilePath);
            string extension = Path.GetExtension(pptFilePath);

            string outputPath = Path.Combine(
                Path.GetDirectoryName(pptFilePath)!,
                $"{originalName}_translated_{targetLang}{extension}"
            );

            if (!File.Exists(pptFilePath))
                throw new FileNotFoundException("Original PPT file not found.", pptFilePath);

            if (!File.Exists(translatedJsonPath))
                throw new FileNotFoundException("Translated JSON file not found.", translatedJsonPath);

            string jsonContent = await File.ReadAllTextAsync(translatedJsonPath);
            var translated = JsonSerializer.Deserialize<TranslatedResult>(jsonContent)
                ?? throw new Exception("Failed to parse translated JSON.");

            ValidateTranslatedJson(translated);

            File.Copy(pptFilePath, outputPath, overwrite: true);
            var pres = new Presentation(outputPath);

            foreach (var item in translated.Items)
            {
                try
                {
                    var slide = pres.Slides[item.SlideIndex - 1];
                    var shape = FindShapeById(slide, item.ShapeId);

                    if (shape?.TextBox != null)
                    {
                        ApplyTranslatedText(shape.TextBox, item.Text);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply translated text on Slide={Slide}, ShapeId={ShapeId}",
                        item.SlideIndex, item.ShapeId);
                }
            }

            pres.Save();
            return outputPath;
        }


        /// <summary>
        /// Inserts translated text into a ShapeCrawler text box while preserving font styling.
        /// </summary>
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
            if (baseFont == null || p.Portions.Count == 0)
                return;

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

        /// <summary>
        /// Validates the structure and values of translated JSON before applying to PPT.
        /// </summary>
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

        /// <summary>
        /// Finds a shape in a slide by its ID.
        /// </summary>
        private IShape? FindShapeById(ISlide slide, string id)
        {
            foreach (var s in slide.Shapes)
            {
                if (s.Id.ToString() == id)
                    return s;
            }
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
