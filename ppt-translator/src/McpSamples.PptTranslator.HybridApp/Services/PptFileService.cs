using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ShapeCrawler;
using ShapeCrawler.Presentations;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public class PptFileService // open&extract, replacement&save
    {
        /// <summary>
        /// PPTX 파일에서 모든 텍스트와 메타데이터(SlideIndex, ShapeId, ShapeName)를 추출하여 문자열 배열로 반환합니다.
        /// </summary>
        /// <param name="filePath">PPTX 파일 경로</param>
        /// <returns>각 텍스트에 대한 메타정보 포함 문자열 배열</returns>
        public async Task<string[]> ExtractAllTextAsync(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            var allTexts = new List<string>();

            try
            {
                var pres = new Presentation(filePath);
                int slideIdx = 0;

                foreach (var slide in pres.Slides)
                {
                    slideIdx++;

                    foreach (var shape in slide.Shapes)
                    {
                        if (shape.TextBox != null && !string.IsNullOrWhiteSpace(shape.TextBox.Text))
                        {
                            string formatted = $"[Slide:{slideIdx}] [Shape:{shape.Id}] [Name:{shape.Name}] {shape.TextBox.Text.Trim()}";
                            allTexts.Add(formatted);
                        }
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error]: {ex.Message}");
                throw;
            }

            return allTexts.ToArray();
        }
    }
}
