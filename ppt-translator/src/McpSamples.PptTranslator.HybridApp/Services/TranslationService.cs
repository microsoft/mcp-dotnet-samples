using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Sas;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang);
    }

    public class TranslationService : ITranslationService
    {
        private readonly ILogger<TranslationService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http = new();
        private readonly BlobServiceClient? _blobServiceClient;

        public TranslationService(
            ILogger<TranslationService> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _http.Timeout = TimeSpan.FromSeconds(300);

            string? conn = config["AzureBlobConnectionString"];
            if (!string.IsNullOrWhiteSpace(conn))
                _blobServiceClient = new BlobServiceClient(conn);
        }

        public async Task<string> TranslateJsonFileAsync(string extractedJsonPath, string targetLang)
        {
            _logger.LogInformation("[STEP 2] Sending translation request.");

            // ============================================================
            // ① Blob URL or local JSON 파일 읽기
            // ============================================================
            string extractedJson;

            if (IsBlobUrl(extractedJsonPath))
            {
                if (_blobServiceClient == null)
                    throw new InvalidOperationException("Blob URL을 처리할 수 없습니다. AzureBlobConnectionString 없음.");

                _logger.LogInformation("[Blob] Extracted JSON 다운로드: {Url}", extractedJsonPath);
                extractedJson = await DownloadBlobAsString(extractedJsonPath);
            }
            else
            {
                if (!File.Exists(extractedJsonPath))
                    throw new FileNotFoundException("Extracted JSON not found.", extractedJsonPath);

                extractedJson = await File.ReadAllTextAsync(extractedJsonPath);
            }

            // ============================================================
            // ② Prompt 로드
            // ============================================================
            string promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "translation_prompt.txt");

            if (!File.Exists(promptPath))
                throw new FileNotFoundException($"Prompt file not found: {promptPath}");

            string promptText = await File.ReadAllTextAsync(promptPath);

            string prompt =
                promptText +
                $"\n\nTARGET_LANG={targetLang}\n\n" +
                extractedJson;

            // ============================================================
            // ③ LLM 요청 준비
            // ============================================================
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

            string translated = 
                doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                ?? throw new Exception("LLM returned empty content.");

            // ============================================================
            // ④ 파일 이름 정의
            // ============================================================
            string fileName = "translated.json";

            // ============================================================
            // ⑤ Blob 업로드 우선 시도
            // ============================================================
            if (_blobServiceClient != null)
            {
                try
                {
                    _logger.LogInformation("[Azure] 번역 JSON Blob Upload 시작");

                    var container = _blobServiceClient.GetBlobContainerClient("generated-files");
                    await container.CreateIfNotExistsAsync();

                    var blob = container.GetBlobClient(fileName);

                    using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(translated));

                    await blob.UploadAsync(uploadStream, overwrite: true);

                    var sasBuilder = new BlobSasBuilder()
                    {
                        BlobContainerName = "generated-files",
                        BlobName = fileName,
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);

                    Uri sasUrl = blob.GenerateSasUri(sasBuilder);

                    _logger.LogInformation("[Azure] 번역 JSON 업로드 성공: {Url}", sasUrl);

                    return sasUrl.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Azure] 업로드 실패 → 로컬 fallback");
                }
            }

            // ============================================================
            // ⑥ Blob 저장 실패 시 로컬 fallback
            // ============================================================
            string localOutput = Path.Combine(
                Path.GetDirectoryName(extractedJsonPath) ?? ".",
                fileName
            );

            await File.WriteAllTextAsync(localOutput, translated, Encoding.UTF8);

            _logger.LogInformation("[Local] 번역 JSON 저장: {Path}", localOutput);

            return localOutput;
        }

        // ============================================================
        // Helper: Blob URL 체크
        // ============================================================
        private bool IsBlobUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        // ============================================================
        // Helper: Blob -> JSON string 다운로드
        // ============================================================
        private async Task<string> DownloadBlobAsString(string blobUrl)
        {
            var uri = new Uri(blobUrl);

            string containerName = uri.Segments[1].Trim('/');
            string blobName = string.Join("", uri.Segments[2..]);

            var container = _blobServiceClient!.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobName);

            if (!await blob.ExistsAsync())
                throw new FileNotFoundException($"Blob not found: {containerName}/{blobName}");

            using var ms = new MemoryStream();
            await blob.DownloadToAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
