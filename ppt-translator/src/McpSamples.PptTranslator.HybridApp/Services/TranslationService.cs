using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    /// <summary>
    /// Sends extracted JSON text to an LLM and returns translated JSON.
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
        private readonly HttpClient _http = new();

        public TranslationService(ILogger<TranslationService> logger)
        {
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(300);
        }

        public async Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang)
        {
            _logger.LogInformation("[STEP 2] Sending translation request.");

            if (!File.Exists(extractedJsonPath))
                throw new FileNotFoundException("Extracted JSON not found.", extractedJsonPath);

            string extractedJson = await File.ReadAllTextAsync(extractedJsonPath);

            // 🔥 FIX: Always load prompt from Prompts folder located in output directory
            string promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "translation_prompt.txt");

            if (!File.Exists(promptPath))
                throw new FileNotFoundException($"Prompt file not found: {promptPath}");

            string promptText = await File.ReadAllTextAsync(promptPath);

            string prompt =
                promptText +
                $"\n\nTARGET_LANG={targetLang}\n\n" +
                extractedJson;

            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new Exception("OPENAI_API_KEY not set.");

            string model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5-nano";
            string endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                ?? "https://api.openai.com/v1/chat/completions";

            var body = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            HttpResponseMessage res = await _http.PostAsync(endpoint, content);
            string raw = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[ERROR] LLM response failed: {Status} / {Body}", res.StatusCode, raw);
                throw new Exception($"Translation failed: {res.StatusCode}");
            }

            using var doc = JsonDocument.Parse(raw);

            string translatedText =
                doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                ?? throw new Exception("LLM returned empty content.");

            string outputPath = Path.Combine(
                Path.GetDirectoryName(extractedJsonPath)!,
                "translated.json"
            );

            await File.WriteAllTextAsync(outputPath, translatedText, Encoding.UTF8);

            _logger.LogInformation("[STEP 2] Translation completed.");

            return outputPath;
        }
    }
}
