using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using McpSamples.PptTranslator.HybridApp.Models;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    /// <summary>
    /// Service for translating extracted text using OpenAI API.
    /// </summary>
    public interface ITranslationService
    {
        /// <summary>
        /// Translates text content from JSON format using OpenAI's language model.
        /// </summary>
        /// <param name="extractedJsonPath">Path to JSON file containing extracted text</param>
        /// <param name="targetLang">Target language code (e.g., 'ko', 'en', 'ja')</param>
        /// <returns>Path to translated JSON file</returns>
        Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang);
    }

    /// <summary>
    /// Default implementation using OpenAI Chat Completions API for translation.
    /// Supports both local and Azure environments with configurable endpoints.
    /// </summary>
    /// <remarks>
    /// OpenAI Chat Completions API를 사용한 번역 서비스 기본 구현.
    /// 로컬 및 Azure 환경에서 설정 가능한 엔드포인트를 지원합니다.
    /// </remarks>
    public class TranslationService : ITranslationService
    {
        private readonly ILogger<TranslationService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http = new();
        private readonly ExecutionMode _executionMode;

        public TranslationService(
            ILogger<TranslationService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _executionMode = ExecutionModeDetector.DetectExecutionMode();
            _http.Timeout = TimeSpan.FromSeconds(300);
        }

        public async Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang)
        {
            _logger.LogInformation("[STEP 2] Sending translation request.");

            // =======================================================
            // Container/Azure 모드 → /files 구조 사용
            // =======================================================
            if (_executionMode.IsContainerMode())
            {
                string jsonFileName = Path.GetFileName(extractedJsonPath);
                string fullPath = Path.Combine("/files/tmp", jsonFileName);

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("JSON not found in /files/tmp.", fullPath);

                string extractedJson = await File.ReadAllTextAsync(fullPath);

                // Prompt 불러오기
                string promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "translation_prompt.txt");
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
                    throw new Exception($"Translation failed: {res.StatusCode}");

                string translated =
                    JsonDocument.Parse(raw)
                        .RootElement.GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString()
                    ?? throw new Exception("LLM returned empty content.");

                // 저장 경로: /files/tmp
                string outputFile = $"{Guid.NewGuid():N}_translated_{targetLang}.json";
                string outputPath = Path.Combine("/files/tmp", outputFile);

                await File.WriteAllTextAsync(outputPath, translated);

                _logger.LogInformation("[Container] 번역 JSON 저장: {Path}", outputPath);

                // 반환: 전체 경로 (다음 단계에서 사용)
                return outputPath;
            }


            // =======================================================
            // Local 모드 (STDIO / HTTP / DOCKER LOCAL) → 파일 처리
            // =======================================================

            if (!File.Exists(extractedJsonPath))
                throw new FileNotFoundException("Extracted JSON not found.", extractedJsonPath);

            string localJson = await File.ReadAllTextAsync(extractedJsonPath);

            string localPromptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "translation_prompt.txt");
            string localPromptText = await File.ReadAllTextAsync(localPromptPath);

            string merged =
                localPromptText +
                $"\n\nTARGET_LANG={targetLang}\n\n" +
                localJson;

            string localApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new Exception("OPENAI_API_KEY not set.");

            string localModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5-nano";
            string localEndpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")
                ?? "https://api.openai.com/v1/chat/completions";

            var localBody = new
            {
                model = localModel,
                messages = new[] { new { role = "user", content = merged } }
            };

            var localContent = new StringContent(JsonSerializer.Serialize(localBody), Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {localApiKey}");

            HttpResponseMessage localRes = await _http.PostAsync(localEndpoint, localContent);
            string localRaw = await localRes.Content.ReadAsStringAsync();

            _logger.LogInformation("[OpenAI Response] Status: {Status}, Body: {Body}", 
                localRes.StatusCode, localRaw);

            if (!localRes.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API failed: {localRes.StatusCode}, Body: {localRaw}");
            }

            string localTranslated =
                JsonDocument.Parse(localRaw)
                    .RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                ?? throw new Exception("LLM returned empty content.");

            string tempDir = Path.Combine(Path.GetTempPath(), "mcp-uploads");
            Directory.CreateDirectory(tempDir);
            string id = Guid.NewGuid().ToString("N");
            string localOutput = Path.Combine(tempDir, id);
            await File.WriteAllTextAsync(localOutput, localTranslated);

            _logger.LogInformation("[Local] 번역 JSON 저장(temp): {Path}", localOutput);

            return localOutput;
        }
    }
}
