using Microsoft.Extensions.Logging;
using McpSamples.PptTranslator.HybridApp.Models;

namespace McpSamples.PptTranslator.HybridApp.Services
{
    /// <summary>
    /// Service for handling file uploads in different execution modes.
    /// </summary>
    public interface IUploadService
    {
        /// <summary>
        /// Saves an uploaded file stream to the appropriate storage location.
        /// </summary>
        /// <param name="stream">File content stream</param>
        /// <param name="fileName">Original filename</param>
        /// <returns>Path where the file was saved</returns>
        Task<string> SaveUploadedFileAsync(Stream stream, string fileName);
    }

    /// <summary>
    /// Default implementation supporting local, container, and Azure storage modes.
    /// Automatically detects execution mode and saves files to appropriate locations.
    /// </summary>
    /// <remarks>
    /// 로컬, 컨테이너, Azure 스토리지 모드를 지원하는 기본 구현.
    /// 실행 모드를 자동으로 감지하고 적절한 위치에 파일을 저장합니다.
    /// </remarks>
    public class UploadService : IUploadService
    {
        private readonly ILogger<UploadService> _logger;
        private readonly ExecutionMode _executionMode;

        // Legacy: Azure 감지 (호환성용)
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
