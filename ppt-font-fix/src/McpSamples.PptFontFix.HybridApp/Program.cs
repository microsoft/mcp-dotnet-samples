using System;
using System.IO;
using System.Threading.Tasks;
using McpSamples.PptFontFix.HybridApp.Services; 
using McpSamples.PptFontFix.HybridApp.Models; 
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

/// <summary>
/// This is the code for testing Ppt font analysis functionality.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("McpSamples.PptFontFix.HybridApp.Services", LogLevel.Information)
                .AddConsole();
        });

        ILogger<PptFontFixService> logger = loggerFactory.CreateLogger<PptFontFixService>();
        IPptFontFixService fontService = new PptFontFixService(logger);

        try
        {
            await fontService.OpenPptFileAsync("test.pptx");

            PptFontAnalyzeResult result = await fontService.AnalyzeFontsAsync();

            // UsedFonts
            Console.WriteLine($"Used (Standard) Fonts ({result.UsedFonts.Count}):");
            if (result.UsedFonts.Any())
                result.UsedFonts.ForEach(font => Console.WriteLine($"- {font}"));
            else
                Console.WriteLine("(None)");

            // UnusedFonts
            Console.WriteLine($"\nUnused Fonts ({result.UnusedFonts.Count}):");
            if (result.UnusedFonts.Any())
                result.UnusedFonts.ForEach(font => Console.WriteLine($"- {font}"));
            else
                Console.WriteLine("(None - All fonts are visibly used in non-empty text boxes)");

            // UnusedFontLocations
            Console.WriteLine($"\n[Locations of Empty/Outside Shapes ({result.UnusedFontLocations.Count})]");
            if (result.UnusedFontLocations.Any())
            {
                foreach (var location in result.UnusedFontLocations)
                {
                    Console.WriteLine($"- Slide {location.SlideNumber}, Shape: '{location.ShapeName}'");
                }
            }
            else
            {
                Console.WriteLine("(None)");
            }

            // InconsistentlyUsedFonts
            Console.WriteLine($"Inconsistently Used Fonts ({result.InconsistentlyUsedFonts.Count}):");
            if (result.InconsistentlyUsedFonts.Any())
            {
                result.InconsistentlyUsedFonts.ForEach(font => Console.WriteLine($"- {font}"));
                Console.WriteLine($"\n[Locations of Inconsistent Fonts ({result.InconsistentFontLocations.Count})]");
                if (result.InconsistentFontLocations.Any())
                {
                    foreach (var location in result.InconsistentFontLocations)
                    {
                        Console.WriteLine($"- Slide {location.SlideNumber}, Shape: '{location.ShapeName}'");
                    }
                }
                else
                {
                    Console.WriteLine("(None)");
                }
            }
            else
            {
                Console.WriteLine("(None)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn error occurred");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
