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

        string testFilePath = "test.pptx";

        var pptService = new PPTFontFixService(logger);

        await pptService.OpenPPTFileAsync(testFilePath);
    }
}