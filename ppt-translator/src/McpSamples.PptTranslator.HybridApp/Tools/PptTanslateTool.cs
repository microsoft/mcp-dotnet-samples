using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpSamples.PptTranslator.HybridApp.Services;
using ModelContextProtocol.Server;

namespace McpSamples.PptTranslator.HybridApp.Tools
{
    /// <summary>
    /// Provides a tool for translating PPT files into another language.
    /// </summary>
    public interface IPptTranslateTool
    {
        Task<string> TranslateAsync(string filePath, string targetLang);
    }

    /// <summary>
    /// Default implementation of PPT translation workflow tool.
    /// </summary>
    [McpServerToolType]
    public class PptTranslateTool : IPptTranslateTool
    {
        private readonly ILogger<PptTranslateTool> _logger;
        private readonly ITextExtractService _extractService;
        private readonly ITranslationService _translationService;
        private readonly IFileRebuildService _rebuildService;

        public PptTranslateTool(
            ILogger<PptTranslateTool> logger,
            ITextExtractService extractService,
            ITranslationService translationService,
            IFileRebuildService rebuildService)
        {
            _logger = logger;
            _extractService = extractService;
            _translationService = translationService;
            _rebuildService = rebuildService;
        }

        [McpServerTool(Name = "translate_ppt_file", Title = "Translate PPT file")]
        [Description("Extracts text from a PPT, translates it using an LLM, and generates a translated PPT file.")]
        public async Task<string> TranslateAsync(
            [Description("Full path of the PPT file to translate")] string filePath,
            [Description("Target language code (e.g., 'ko', 'en', 'ja')")] string targetLang)
        {
            string step = "INITIAL";

            try
            {
                _logger.LogInformation("PPT translation started.");
                _logger.LogInformation("File: {File}", filePath);
                _logger.LogInformation("Target language: {Lang}", targetLang);

                if (string.IsNullOrWhiteSpace(targetLang))
                    targetLang = "ko";

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("PPT file not found.", filePath);

                string directory = Path.GetDirectoryName(filePath)!;

                // Extract text
                step = "TEXT_EXTRACTION";
                _logger.LogInformation("==========[ STEP 1: TEXT EXTRACTION ]==========");
                await _extractService.OpenPptFileAsync(filePath);
                var extracted = await _extractService.TextExtractAsync();

                string extractedJsonPath = Path.Combine(directory, "extracted.json");
                await _extractService.ExtractToJsonAsync(extracted, extractedJsonPath);
                _logger.LogInformation("Text extraction completed.");

                // Translation
                step = "TRANSLATION";
                _logger.LogInformation("==========[ STEP 2: TRANSLATION ]==========");
                string translatedJsonPath =
                    await _translationService.TranslateJsonFileAsync(extractedJsonPath, targetLang);
                _logger.LogInformation("Translation completed.");

                // Rebuild PPT
                step = "REBUILD";
                _logger.LogInformation("==========[ STEP 3: REBUILD PPT ]==========");
                string outputPptPath =
                    await _rebuildService.RebuildPptFromJsonAsync(filePath, translatedJsonPath);

                _logger.LogInformation("PPT translation finished: {Output}", outputPptPath);
                return $"Translated PPT generated: {outputPptPath}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error occurred. Step = {Step}, File = {FilePath}",
                    step,
                    filePath);

                return $"Error at step '{step}': {ex.Message}";
            }
        }
    }
}
