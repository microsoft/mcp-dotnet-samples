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
        /// <summary>
        /// Applies translated text (JSON) to the original PPT and produces a rebuilt PPT file.
        /// </summary>
        Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath);
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

        /// <inheritdoc />
        public async Task<string> RebuildPptFromJsonAsync(string pptFilePath, string translatedJsonPath)
        {
            if (!File.Exists(pptFilePath))
                throw new FileNotFoundException("Original PPT file not found.", pptFilePath);

            if (!File.Exists(translatedJsonPath))
                throw new FileNotFoundException("Translated JSON file not found.", translatedJsonPath);

            _logger.LogInformation("PPT rebuild started.");

            string jsonContent = await File.ReadAllTextAsync(translatedJsonPath);
            var translated = JsonSerializer.Deserialize<TranslatedResult>(jsonContent);

            if (translated?.Items == null)
                throw new Exception("Invalid translated JSON structure.");

            string outputPath = Path.Combine(
                Path.GetDirectoryName(pptFilePath)!,
                "translated_output.pptx"
            );

            // Output PPT 파일 생성
            File.Copy(pptFilePath, outputPath, overwrite: true);

            var pres = new Presentation(outputPath);

            // 각 텍스트 항목을 PPT에 적용
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
                    _logger.LogError(ex,
                        $"Error applying text for Slide {item.SlideIndex}, ShapeId {item.ShapeId}");
                }
            }

            pres.Save();
            _logger.LogInformation("PPT rebuild completed.");

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

            // 기본 스타일은 첫 Portion에서 가져온다.
            ITextPortionFont? baseFont =
                firstParagraph.Portions.Count > 0 ? firstParagraph.Portions[0].Font : null;

            // Paragraph를 완전히 초기화한다 (내부 XML 충돌 방지).
            firstParagraph.Text = string.Empty;

            var lines = translatedText.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None
            );

            // 첫 줄 처리
            firstParagraph.Portions.AddText(lines[0]);
            ApplyStyleIfAvailable(firstParagraph, baseFont);

            // 기존 Paragraph는 재사용하되 텍스트만 초기화한다.
            for (int i = 1; i < paragraphs.Count; i++)
                paragraphs[i].Text = string.Empty;

            // 나머지 줄 반영
            for (int i = 1; i < lines.Length; i++)
            {
                IParagraph paragraph;

                if (i < paragraphs.Count)
                {
                    paragraph = paragraphs[i];
                }
                else
                {
                    paragraphs.Add();
                    paragraph = paragraphs[i];
                }

                paragraph.Text = lines[i];
                ApplyStyleIfAvailable(paragraph, baseFont);
            }
        }

        /// <summary>
        /// Copies font styling into the last portion of a paragraph.
        /// </summary>
        private void ApplyStyleIfAvailable(IParagraph paragraph, ITextPortionFont? baseFont)
        {
            if (baseFont == null || paragraph.Portions.Count == 0)
                return;

            var portionFont = paragraph.Portions.Last().Font;
            if (portionFont == null)
                return;

            // 스타일 복사 (굵기, 기울임, 언더라인, 색상, 폰트명 등)
            portionFont.Size = baseFont.Size;
            portionFont.IsBold = baseFont.IsBold;
            portionFont.IsItalic = baseFont.IsItalic;
            portionFont.Underline = baseFont.Underline;
            portionFont.Color.Set(baseFont.Color.Hex);

            if (baseFont.LatinName != null)
                portionFont.LatinName = baseFont.LatinName;

            portionFont.EastAsianName = baseFont.EastAsianName;
        }

        /// <summary>
        /// Finds a shape on a slide using its ShapeId.
        /// Prefers shapes that contain a TextBox to avoid selecting non-text shapes.
        /// </summary>
        private static IShape? FindShapeById(ISlide slide, string shapeId)
        {
            IShape? bestCandidate = null;

            foreach (var shape in slide.Shapes)
            {
                if (shape.Id.ToString() != shapeId)
                    continue;

                // 우선순위: TextBox가 있는 shape
                if (shape.TextBox != null)
                    return shape;

                // 텍스트 없는 경우 후보로 저장
                if (bestCandidate == null)
                    bestCandidate = shape;
            }

            // 텍스트 없는 shape여도 fallback으로 반환
            return bestCandidate;
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
