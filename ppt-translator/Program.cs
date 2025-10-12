using System;
using McpSamples.PptTranslator.HybridApp.Services;

class Program
{
    static void Main()
    {
        string filePath = "TestFiles/sample.pptx";
        var pptService = new PptLoadService();

        int slideCount = pptService.GetSlideCount(filePath);
        Console.WriteLine($"슬라이드 수: {slideCount}");
    }
}
