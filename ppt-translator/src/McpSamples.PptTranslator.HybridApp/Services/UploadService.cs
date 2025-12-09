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



        private const string InputMountPath = "/files/input";

        public UploadService(ILogger<UploadService> logger)
        {
            _logger = logger;
            _executionMode = ExecutionModeDetector.DetectExecutionMode();
            
            _logger.LogInformation("[UploadService] ExecutionMode: {Mode}", _executionMode);
        }

        public async Task<string> SaveUploadedFileAsync(Stream stream, string fileName)
        {
            if (_executionMode.IsContainerMode())
            {
                // Container/Azure 모드: 통합된 /files/input 사용
                Directory.CreateDirectory(InputMountPath);
                string savePath = Path.Combine(InputMountPath, fileName); // 원본 파일명 사용
                using (var fs = File.Create(savePath))
                    await stream.CopyToAsync(fs);
                _logger.LogInformation("[UPLOAD] Saved {FileName} → {Path}", fileName, savePath);
                return fileName; // 원본 파일명 반환
            }
            else
            {
                // 로컬 모드: 업로드 기능 미지원
                throw new InvalidOperationException("Local mode does not support file upload. Please provide absolute file path directly.");
            }
        }
    }
}
