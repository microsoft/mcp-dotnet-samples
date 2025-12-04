using Microsoft.Extensions.Logging;
using McpSamples.PptTranslator.HybridApp.Models;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    public interface IUploadService
    {
        Task<string> SaveUploadedFileAsync(Stream stream, string fileName);
    }

    public class UploadService : IUploadService
    {
        private readonly ILogger<UploadService> _logger;
        private readonly ExecutionMode _executionMode;

        // Azure 감지 (legacy, 호환성용)
        private readonly bool _isAzure =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));

        private const string AzureInputMountPath = "/mnt/storage/input";
        private const string ContainerInputMountPath = "/mnt/input";

        public UploadService(ILogger<UploadService> logger)
        {
            _logger = logger;
            _executionMode = ExecutionModeDetector.DetectExecutionMode();
            
            _logger.LogInformation("[UploadService] ExecutionMode: {Mode}", _executionMode);
        }

        public async Task<string> SaveUploadedFileAsync(Stream stream, string fileName)
        {
            string id = Guid.NewGuid().ToString("N");

            switch (_executionMode)
            {
                case ExecutionMode.HttpRemote:
                    // Azure 모드: /mnt/storage/input에 저장
                    Directory.CreateDirectory(AzureInputMountPath);
                    string azureSavePath = Path.Combine(AzureInputMountPath, $"{id}_{fileName}");
                    using (var azureFs = File.Create(azureSavePath))
                        await stream.CopyToAsync(azureFs);
                    _logger.LogInformation("[UPLOAD/Azure] Saved {FileName} → {Path}", fileName, azureSavePath);
                    return Path.GetFileName(azureSavePath);

                case ExecutionMode.HttpContainer:
                    // Container 모드: /mnt/input에 저장
                    Directory.CreateDirectory(ContainerInputMountPath);
                    string containerSavePath = Path.Combine(ContainerInputMountPath, fileName);
                    using (var containerFs = File.Create(containerSavePath))
                        await stream.CopyToAsync(containerFs);
                    _logger.LogInformation("[UPLOAD/Container] Saved {FileName} → {Path}", fileName, containerSavePath);
                    return fileName;

                case ExecutionMode.HttpLocal:
                case ExecutionMode.StdioLocal:
                default:
                    // 로컬 모드: 파일명 그대로 반환 (업로드 기능 미사용)
                    throw new InvalidOperationException("Local mode does not support file upload. Please provide absolute file path directly.");
            }
        }
    }
}
