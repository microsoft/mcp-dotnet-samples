using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using McpSamples.PptTranslator.HybridApp.Services;
using McpSamples.PptTranslator.HybridApp.Models;
using ModelContextProtocol.Server;

namespace McpSamples.PptTranslator.HybridApp.Tools
{
    /// <summary>
    /// Provides a tool for translating PPT files into another language.
    /// </summary>
    public interface IPptTranslateTool
    {
        Task<string> TranslateAsync(string filePath, string targetLang, string? outputPath = null);
    }

    /// <summary>
    /// Default implementation of PPT translation workflow tool.
    /// Supports: local file, container volume, Azure Blob URL
    /// </summary>
    [McpServerToolType]
    public class PptTranslateTool : IPptTranslateTool
    {
        private readonly ILogger<PptTranslateTool> _logger;
        private readonly ITextExtractService _extractService;
        private readonly ITranslationService _translationService;
        private readonly IFileRebuildService _rebuildService;
        private readonly IUploadService _uploadService;
        private readonly ExecutionMode _executionMode;


        public PptTranslateTool(
            ILogger<PptTranslateTool> logger,
            ITextExtractService extractService,
            ITranslationService translationService,
            IFileRebuildService rebuildService,
            IUploadService uploadService)
        {
            _logger = logger;
            _extractService = extractService;
            _translationService = translationService;
            _rebuildService = rebuildService;
            _uploadService = uploadService;
            _executionMode = ExecutionModeDetector.DetectExecutionMode();
            
            _logger.LogInformation("[ExecutionMode] Detected: {Mode}", _executionMode);
        }


        [McpServerTool(Name = "translate_ppt_file")]
        [Description("Translates a PPT file into the specified target language.")]
        public async Task<string> TranslateAsync(
            [Description("Path to the PPT file to translate")] string filePath,
            [Description("Target language code (e.g., 'ko', 'en', 'ja')")] string targetLang,
            [Description("(Optional) Absolute path to directory where translated file should be saved. If provided in container mode, a copy command will be returned.")] string? outputPath = null)
        {
            string step = "INITIAL";

            try
            {
                if (string.IsNullOrWhiteSpace(targetLang))
                    targetLang = "ko";

                // -----------------------------
                // STEP 0: 입력 경로 처리 (모드별)
                // -----------------------------
                string resolvedInputPath = await ResolveInputPathAsync(filePath);
                string originalFileName = Path.GetFileName(filePath);

                // -----------------------------
                // STEP 1: Extract
                // -----------------------------
                step = "extract";
                _logger.LogInformation("[STEP 1] Extracting text from: {Path}", resolvedInputPath);

                await _extractService.OpenPptFileAsync(resolvedInputPath);
                var extracted = await _extractService.TextExtractAsync();

                // 작업 디렉토리 결정 (모드별)
                string workDir = _executionMode.IsContainerMode()
                    ? "/files/tmp"  // Container/Azure 모드: 통합된 /files/tmp 사용
                    : Path.GetDirectoryName(resolvedInputPath) ?? Path.Combine(Path.GetTempPath(), "ppt-translator");

                Directory.CreateDirectory(workDir);

                string extractedJsonPath = Path.Combine(workDir, "extracted.json");
                await _extractService.ExtractToJsonAsync(extracted, extractedJsonPath);

                // -----------------------------
                // STEP 2: Translate
                // -----------------------------
                step = "translate";
                string translatedJsonPath =
                    await _translationService.TranslateJsonFileAsync(extractedJsonPath, targetLang);

                // -----------------------------
                // STEP 3: Rebuild PPT
                // -----------------------------
                step = "rebuild";
                
                // 출력 경로 결정 (모드별)
                string finalOutputPath = DetermineOutputPath(originalFileName, targetLang, outputPath);
                
                string output =
                    await _rebuildService.RebuildPptFromJsonAsync(resolvedInputPath, translatedJsonPath, targetLang, finalOutputPath);

                return BuildSuccessMessage(output, originalFileName, targetLang, outputPath);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("AGENT_ACTION_REQUIRED"))
            {
                // 에이전트가 수행해야 할 작업이 있는 경우 (예: 파일 복사)
                _logger.LogInformation("[Container] Agent action required: {Message}", ex.Message);
                return ex.Message.Replace("AGENT_ACTION_REQUIRED: ", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERROR] STEP={Step}: {Message}", step, ex.Message);
                return $"Error at step '{step}': {ex.Message}";
            }
        }

        /// <summary>
        /// Resolves the input file path based on current execution mode.
        /// Handles path translation between local, container, and Azure environments.
        /// </summary>
        /// <param name="filePath">User-provided file path</param>
        /// <returns>Resolved absolute path accessible in current environment</returns>
        /// <exception cref="FileNotFoundException">When file cannot be found in expected location</exception>
        /// <exception cref="InvalidOperationException">When file requires upload or copy action</exception>
        /// <remarks>
        /// 현재 실행 모드에 따라 입력 파일 경로를 해석합니다.
        /// 로컬, 컨테이너, Azure 환경 간 경로 변환을 처리합니다.
        /// </remarks>
        private async Task<string> ResolveInputPathAsync(string filePath)
        {
            // 로컬 모드 vs 컨테이너 모드로 단순화
            if (_executionMode.IsLocalMode())
            {
                return ResolveLocalFilePath(filePath);
            }
            else if (_executionMode.IsContainerMode())
            {
                return await ResolveContainerFilePath(filePath);
            }
            else
            {
                throw new InvalidOperationException($"Unknown execution mode: {_executionMode}");
            }
        }

        /// <summary>
        /// Determines the output path for translated file based on execution mode.
        /// Respects user-provided output path when applicable.
        /// </summary>
        /// <param name="originalFileName">Original input filename</param>
        /// <param name="targetLang">Target language code for filename suffix</param>
        /// <param name="userOutputPath">Optional user-specified output directory</param>
        /// <returns>Full path where translated file should be saved</returns>
        /// <remarks>
        /// 실행 모드에 따라 번역된 파일의 출력 경로를 결정합니다.
        /// 사용자가 제공한 출력 경로가 있는 경우 이를 우선합니다.
        /// </remarks>
        private string DetermineOutputPath(string originalFileName, string targetLang, string? userOutputPath)
        {
            string outputFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_{targetLang}.pptx";

            if (_executionMode.IsLocalMode())
            {
                return DetermineLocalOutputPath(outputFileName, userOutputPath);
            }
            else if (_executionMode.IsContainerMode())
            {
                // Container/Azure 모드: 통합된 /files/output 사용
                string outputDir = "/files/output";
                Directory.CreateDirectory(outputDir);
                return Path.Combine(outputDir, outputFileName);
            }
            else
            {
                throw new InvalidOperationException($"Unknown execution mode: {_executionMode}");
            }
        }

        #region Helper Methods for Path Resolution

        /// <summary>
        /// 로컬 모드에서 파일 경로
        /// </summary>
        private string ResolveLocalFilePath(string filePath)
        {
            if (Path.IsPathRooted(filePath) && File.Exists(filePath))
            {
                return filePath;
            }
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        /// <summary>
        /// 컨테이너/Azure 모드에서 파일 경로
        /// </summary>
        private async Task<string> ResolveContainerFilePath(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string inputDir = "/files/input";
            string inputPath = Path.Combine(inputDir, fileName);
            
            Directory.CreateDirectory(inputDir);
            
            // 1. 먼저 /files/input에서 파일 찾기
            if (File.Exists(inputPath))
            {
                _logger.LogInformation("[Container] File found in input folder: {Path}", inputPath);
                return inputPath;
            }
            
            // 2. /files에서 직접 업로드된 파일 찾기 (Azure 업로드 케이스)
            string directFilePath = Path.Combine("/files", fileName);
            if (File.Exists(directFilePath))
            {
                _logger.LogInformation("[Container] File found in files root: {Path}", directFilePath);
                // 파일을 input 폴더로 이동하여 일관성 유지
                try
                {
                    File.Move(directFilePath, inputPath);
                    _logger.LogInformation("[Container] File moved from {Source} to {Target}", directFilePath, inputPath);
                    return inputPath;
                }
                catch (Exception moveEx)
                {
                    _logger.LogWarning(moveEx, "[Container] Failed to move file, using original location");
                    return directFilePath;
                }
            }
            
            // 3. 파일을 찾을 수 없는 경우 업로드/복사 처리
            if (_executionMode == ExecutionMode.HttpRemote)
            {
                return await HandleAzureFileUpload(filePath, fileName, inputPath);
            }
            else
            {
                return HandleContainerFileCopy(filePath, fileName, inputPath);
            }
        }

        /// <summary>
        /// Azure 모드에서 파일 업로드를 처리합니다.
        /// </summary>
        private async Task<string> HandleAzureFileUpload(string filePath, string fileName, string inputPath)
        {
            _logger.LogWarning("[Azure] File not found in Azure File Share. Attempting automatic upload.");
            
            try
            {
                using var fileStream = File.OpenRead(filePath);
                string uploadResult = await _uploadService.SaveUploadedFileAsync(fileStream, fileName);
                _logger.LogInformation("[Azure] File automatically uploaded: {Result}", uploadResult);
                
                if (File.Exists(inputPath))
                {
                    return inputPath;
                }
                else
                {
                    _logger.LogWarning("[Azure] File uploaded but not found in expected location");
                    throw new InvalidOperationException($"File uploaded but not accessible at {inputPath}");
                }
            }
            catch (Exception uploadEx)
            {
                _logger.LogError(uploadEx, "[Azure] Failed to auto-upload file. Providing manual instructions.");
                
                string? hostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME") 
                    ?? "ppt-translator.braveriver-9b702e70.southeastasia.azurecontainerapps.io";
                
                string uploadCommands = $"""
                    Automatic upload failed. Please upload the file manually:

                    🍎/🐧 macOS & Linux:
                        curl -F "file=@{filePath}" https://{hostname}/upload

                    💻 Windows (Command Prompt/PowerShell):
                        curl.exe -F "file=@{filePath}" https://{hostname}/upload
                    """;
                
                throw new InvalidOperationException(
                    $"AGENT_ACTION_REQUIRED: {uploadCommands}\n\n" +
                    $"After successful upload, call translate_ppt_file again with filePath=\"{fileName}\" (filename only).\n\n" +
                    $"Expected location in Azure: /files/{fileName}");
            }
        }

        /// <summary>
        /// 컨테이너 모드에서 파일 복사를 처리합니다.
        /// </summary>
        private string HandleContainerFileCopy(string filePath, string fileName, string inputPath)
        {
            string? hostMountPath = ExecutionModeDetector.GetHostMountPath();
            if (string.IsNullOrEmpty(hostMountPath))
            {
                _logger.LogError("[Container] HOST_MOUNT_PATH environment variable not set");
                throw new InvalidOperationException(
                    "AGENT_ACTION_REQUIRED: HOST_MOUNT_PATH environment variable is not configured.\n\n" +
                    "Please ensure the MCP configuration includes:\n" +
                    "-e HOST_MOUNT_PATH=${input:ppt-folder-path}\n\n" +
                    $"Then copy the file to the mounted folder and call translate_ppt_file with filePath=\"{fileName}\"");
            }
            
            _logger.LogWarning("[Container] File not in input folder. Auto-copying file to mounted input folder.");
            string hostInputDir = Path.Combine(hostMountPath, "input");
            string targetPath = Path.Combine(hostInputDir, fileName);
            
            try 
            {
                Directory.CreateDirectory(hostInputDir);
                File.Copy(filePath, targetPath, overwrite: true);
                _logger.LogInformation("[Container] File automatically copied from {Source} to {Target}", filePath, targetPath);
                return inputPath;
            }
            catch (Exception copyEx)
            {
                _logger.LogError(copyEx, "[Container] Failed to auto-copy file. Providing manual instructions.");
                
                string copyCommands = $"""
                    Automatic file copy failed. Please copy the file to the input folder manually:

                    🍎/🐧 macOS & Linux:
                        cp "{filePath}" "{targetPath}"

                    💻 Windows Command Prompt:
                        copy "{filePath}" "{targetPath}"

                    💻 Windows PowerShell:
                        Copy-Item "{filePath}" -Destination "{targetPath}"
                    """;
                
                throw new InvalidOperationException(
                    $"AGENT_ACTION_REQUIRED: {copyCommands}\n\n" +
                    $"Then call translate_ppt_file again with filePath=\"{fileName}\"");
            }
        }

        /// <summary>
        /// 로컬 모드에서 출력 경로를 결정합니다.
        /// </summary>
        private string DetermineLocalOutputPath(string outputFileName, string? userOutputPath)
        {
            if (!string.IsNullOrWhiteSpace(userOutputPath))
            {
                if (!Path.IsPathRooted(userOutputPath))
                {
                    throw new ArgumentException("outputPath must be an absolute path");
                }
                Directory.CreateDirectory(userOutputPath);
                return Path.Combine(userOutputPath, outputFileName);
            }
            
            string projectRoot = Directory.GetCurrentDirectory();
            string defaultOutputDir = Path.Combine(projectRoot, "wwwroot", "generated");
            Directory.CreateDirectory(defaultOutputDir);
            return Path.Combine(defaultOutputDir, outputFileName);
        }

        #endregion

        /// <summary>
        /// Builds a user-friendly success message with file access instructions.
        /// Message format varies by execution mode to provide appropriate file retrieval steps.
        /// </summary>
        /// <param name="outputPath">Path where translated file was saved</param>
        /// <param name="originalFileName">Original input filename</param>
        /// <param name="targetLang">Target language code</param>
        /// <param name="userOutputPath">User-provided output path if any</param>
        /// <returns>Formatted success message with file location and access instructions</returns>
        /// <remarks>
        /// 파일 접근 방법을 포함한 사용자 친화적인 성공 메시지를 생성합니다.
        /// 메시지 형식은 실행 모드에 따라 달라지며 적절한 파일 다운로드 방법을 제공합니다.
        /// </remarks>
        private string BuildSuccessMessage(string outputPath, string originalFileName, string targetLang, string? userOutputPath)
        {
            string outputFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_{targetLang}.pptx";

            if (_executionMode == ExecutionMode.StdioLocal)
            {
                return $"Translation complete!\nOutput file: {outputPath}";
            }
            else if (_executionMode == ExecutionMode.HttpLocal)
            {
                string downloadUrl = $"http://localhost:5166/download/{outputFileName}";
                return $"""
                    Translation complete!
                    📂 Local file: {outputPath}
                    🔗 Download URL: {downloadUrl}
                    
                    💡 Access via browser or curl:
                    curl -o "{outputFileName}" {downloadUrl}
                    """;
            }
            else if (_executionMode.IsContainerMode())
            {
                return BuildContainerSuccessMessage(outputFileName);
            }
            else
            {
                return $"Translation complete!\nOutput: {outputPath}";
            }
        }

        /// <summary>
        /// Container/Azure 모드에서 성공 메시지를 생성합니다.
        /// </summary>
        private string BuildContainerSuccessMessage(string outputFileName)
        {
            string? hostMountPath = ExecutionModeDetector.GetHostMountPath();
            
            // HTTP 모드인지 확인
            bool isHttpMode = _executionMode == ExecutionMode.HttpContainer || _executionMode == ExecutionMode.HttpRemote;
            
            if (isHttpMode)
            {
                return BuildHttpContainerMessage(outputFileName, hostMountPath);
            }
            else
            {
                return BuildStdioContainerMessage(outputFileName, hostMountPath);
            }
        }

        /// <summary>
        /// HTTP 컨테이너 모드에서 성공 메시지를 생성합니다.
        /// </summary>
        private string BuildHttpContainerMessage(string outputFileName, string? hostMountPath)
        {
            if (_executionMode == ExecutionMode.HttpRemote)
            {
                // Azure 모드: Container App FQDN 사용
                string? containerAppHostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
                string downloadUrl = !string.IsNullOrEmpty(containerAppHostname)
                    ? $"https://{containerAppHostname}/download/{outputFileName}"
                    : $"http://localhost:8080/download/{outputFileName}";
                
                return $@"Translation complete!

Download your file:
{downloadUrl}

Or use curl:
curl -o ""{outputFileName}"" {downloadUrl}";
            }
            else
            {
                // HTTP Container 모드
                if (string.IsNullOrEmpty(hostMountPath))
                {
                    return $@"Translation complete!

Download your file:
http://localhost:8080/download/{outputFileName}

Or use curl:
curl -o ""{outputFileName}"" http://localhost:8080/download/{outputFileName}";
                }
                
                string hostOutputFile = Path.Combine(hostMountPath, "output", outputFileName);
                
                return $@"Translation complete!

Download your file:
http://localhost:8080/download/{outputFileName}

Or use curl:
curl -o ""{outputFileName}"" http://localhost:8080/download/{outputFileName}

File is also available at: {hostOutputFile}";
            }
        }

        /// <summary>
        /// STDIO 컨테이너 모드에서 성공 메시지를 생성합니다.
        /// </summary>
        private string BuildStdioContainerMessage(string outputFileName, string? hostMountPath)
        {
            if (string.IsNullOrEmpty(hostMountPath))
            {
                return $"Translation complete!\nOutput file: /files/output/{outputFileName}\n\nNote: The file is in the container's /files/output folder.";
            }
            
            string hostOutputFile = Path.Combine(hostMountPath, "output", outputFileName);
            
            return $@"Translation complete!

Output file is ready at:
{hostOutputFile}

If you want to copy the file to a different location, you can use:

🍎/🐧 macOS & Linux:
    cp ""{hostOutputFile}"" ""/path/to/destination/{outputFileName}""

💻 Windows Command Prompt:
    copy ""{hostOutputFile}"" ""\\path\\to\\destination\\{outputFileName}""

💻 Windows PowerShell:
    Copy-Item ""{hostOutputFile}"" -Destination ""/path/to/destination/{outputFileName}""";
        }
    }
}
