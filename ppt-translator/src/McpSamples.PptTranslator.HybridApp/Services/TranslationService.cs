using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpSamples.PptTranslator.HybridApp.Prompts;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    /// <summary>
    /// Provides translation using an external LLM.
    /// </summary>
    public interface ITranslationService
    {
        Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang);
    }

    /// <summary>
    /// Default translation service implementation using OpenAI-compatible APIs.
    /// </summary>
    public class TranslationService : ITranslationService
    {
        private readonly ILogger<TranslationService> _logger;
        private readonly ITranslationPrompt _prompt;
        private readonly HttpClient _http = new();

        public TranslationService(
            ILogger<TranslationService> logger,
            ITranslationPrompt prompt)
        {
            _logger = logger;
            _prompt = prompt;
        }

        /// <inheritdoc />
        public async Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang)
        {
            if (!File.Exists(extractedJsonPath))
                throw new FileNotFoundException("Extracted JSON file not found.", extractedJsonPath);

            string extractedJsonText = await File.ReadAllTextAsync(extractedJsonPath);
            string directory = Path.GetDirectoryName(extractedJsonPath)!;
            string outputPath = Path.Combine(directory, "translated.json");

            // Build prompt
            string translationPrompt = _prompt.GetTranslationPrompt(targetLang) + "\n\n" + extractedJsonText;

            _logger.LogInformation("Sending translation request...");

            // Load OpenAI config
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new Exception("Environment variable OPENAI_API_KEY is not set.");

            string model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5-nano";
            string endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                ?? "https://api.openai.com/v1/chat/completions";

            _logger.LogInformation("Using model: {Model}", model);

            // Build request body
            var body = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = translationPrompt }
                }
            };

            var httpContent = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // Send request
            HttpResponseMessage response = await _http.PostAsync(endpoint, httpContent);
            string rawResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Translation failed: {Response}", rawResponse);
                throw new Exception($"LLM Error: {response.StatusCode}");
            }

            // Extract translated content
            using var doc = JsonDocument.Parse(rawResponse);
            string translatedText =
                doc.RootElement
                   .GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString()
                   ?? throw new Exception("LLM returned empty content.");

            // Save output
            await File.WriteAllTextAsync(outputPath, translatedText, Encoding.UTF8);

            _logger.LogInformation("Translated JSON saved: {Path}", outputPath);

            return outputPath;
        }
    }
}
