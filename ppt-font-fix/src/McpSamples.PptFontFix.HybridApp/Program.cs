using System;
using System.IO;
using System.Threading.Tasks;
using McpSamples.PptFontFix.HybridApp.Services;

using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        ILogger<PPTFontFixService> logger = loggerFactory.CreateLogger<PPTFontFixService>();

        // test.pptx file is normal ppt file for testing
        // corrupt_test.pptx file is a corrupted ppt file for testing
        string testFilePath = "test.pptx";
        string corruptTestFilePath = "corrupt_test.pptx";

        var pptService = new PPTFontFixService(logger);

        await pptService.OpenPPTFileAsync(testFilePath);
        await pptService.OpenPPTFileAsync(corruptTestFilePath);
    }
}