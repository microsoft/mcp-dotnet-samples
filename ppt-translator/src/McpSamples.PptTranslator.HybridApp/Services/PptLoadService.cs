using System;
using System.IO;
using ShapeCrawler;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public class PptLoadService
    {
        /// PPTX 파일을 열고 슬라이드 수를 반환
        public int GetSlideCount(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File does not exist!", filePath);
            }

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            var presentation = SCPresentation.Open(stream);

            return presentation.Slides.Count;
        }
    }
}