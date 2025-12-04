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
                string resolvedInputPath = ResolveInputPath(filePath);
                string originalFileName = Path.GetFileName(filePath);

                // -----------------------------
                // STEP 1: Extract
                // -----------------------------
                step = "extract";
                _logger.LogInformation("[STEP 1] Extracting text from: {Path}", resolvedInputPath);

                await _extractService.OpenPptFileAsync(resolvedInputPath);
                var extracted = await _extractService.TextExtractAsync();

                string workDir = Path.GetDirectoryName(resolvedInputPath) 
                    ?? Path.Combine(Path.GetTempPath(), "ppt-translator");

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
                _logger.LogInformation("[DEBUG] User outputPath: {OutputPath}", outputPath ?? "null");
                string finalOutputPath = DetermineOutputPath(originalFileName, targetLang, outputPath);
                _logger.LogInformation("[DEBUG] Final outputPath: {FinalPath}", finalOutputPath);
                
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
        /// Resolves the input file path based on execution mode.
        /// </summary>
        private string ResolveInputPath(string filePath)
        {
            switch (_executionMode)
            {
                case ExecutionMode.StdioLocal:
                case ExecutionMode.HttpLocal:
                    // 로컬 모드: 절대 경로 그대로 사용
                    if (Path.IsPathRooted(filePath) && File.Exists(filePath))
                    {
                        return filePath;
                    }
                    throw new FileNotFoundException($"File not found: {filePath}");

                case ExecutionMode.StdioContainer:
                case ExecutionMode.HttpContainer:
                    // Container 모드: /files/{filename} 사용
                    string fileName = Path.GetFileName(filePath);
                    string containerInputPath = Path.Combine("/files", fileName);
                    
                    // 파일이 이미 마운트 폴더에 있으면 진행
                    if (File.Exists(containerInputPath))
                    {
                        _logger.LogInformation("[Container] File already in mount folder: {Path}", containerInputPath);
                        return containerInputPath;
                    }
                    
                    // 파일이 없으면 로컬 경로로 제공된 것 -> 에이전트가 복사해야 함
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
                    
                    _logger.LogWarning("[Container] File not in mount folder. Requires agent to copy file.");
                    string targetPath = Path.Combine(hostMountPath, fileName);
                    string copyCommand = $"cp \"{filePath}\" \"{targetPath}\"";
                    
                    throw new InvalidOperationException(
                        $"AGENT_ACTION_REQUIRED: Please copy the file to the mounted folder first.\n\n" +
                        $"Run this command:\n{copyCommand}\n\n" +
                        $"Then call translate_ppt_file again with filePath=\"{fileName}\"");;

                case ExecutionMode.HttpRemote:
                    // Azure 모드: /files/{filename} 확인
                    string azureFileName = Path.GetFileName(filePath);
                    string azureInputPath = Path.Combine("/files", azureFileName);
                    
                    if (File.Exists(azureInputPath))
                    {
                        _logger.LogInformation("[Azure] File found in mounted storage: {Path}", azureInputPath);
                        return azureInputPath;
                    }
                    
                    _logger.LogError("[Azure] File not found in Azure File Share: {Path}", azureInputPath);
                    
                    // 에이전트에게 업로드 명령
                    string? hostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
                    if (string.IsNullOrEmpty(hostname))
                    {
                        hostname = "ppt-translator.braveriver-9b702e70.southeastasia.azurecontainerapps.io"; // fallback
                    }
                    
                    string uploadCommand = $"curl -F \"file=@{filePath}\" https://{hostname}/upload";
                    
                    throw new InvalidOperationException(
                        $"AGENT_ACTION_REQUIRED: File not found in Azure File Share.\n\n" +
                        $"Please upload the file first by running this command:\n\n" +
                        $"{uploadCommand}\n\n" +
                        $"After successful upload, call translate_ppt_file again with filePath=\"{azureFileName}\" (filename only).\n\n" +
                        $"Expected location in Azure: /files/{azureFileName}");

                default:
                    throw new InvalidOperationException($"Unknown execution mode: {_executionMode}");
            }
        }

        private string DetermineOutputPath(string originalFileName, string targetLang, string? userOutputPath)
        {
            string outputFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_{targetLang}.pptx";

            switch (_executionMode)
            {
                case ExecutionMode.StdioLocal:
                case ExecutionMode.HttpLocal:
                    // 로컬 모드: 사용자 지정 경로 또는 wwwroot/generated
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

                case ExecutionMode.StdioContainer:
                case ExecutionMode.HttpContainer:
                    // Container 모드: /files에 생성 (입력과 같은 폴더)
                    string containerOutputDir = "/files";
                    return Path.Combine(containerOutputDir, outputFileName);

                case ExecutionMode.HttpRemote:
                    // Azure 모드: /files에 생성 (container와 동일)
                    string azureOutputDir = "/files";
                    return Path.Combine(azureOutputDir, outputFileName);

                default:
                    throw new InvalidOperationException($"Unknown execution mode: {_executionMode}");
            }
        }

        /// <summary>
        /// Builds the success message based on execution mode.
        /// </summary>
        private string BuildSuccessMessage(string outputPath, string originalFileName, string targetLang, string? userOutputPath)
        {
            string outputFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_{targetLang}.pptx";

            switch (_executionMode)
            {
                case ExecutionMode.StdioLocal:
                case ExecutionMode.HttpLocal:
                    return $"Translation complete!\nOutput file: {outputPath}";

                case ExecutionMode.StdioContainer:
                    // STDIO Container 모드: 마운트 폴더 경로 확인
                    string? hostMountPathStdio = ExecutionModeDetector.GetHostMountPath();
                    
                    if (string.IsNullOrEmpty(hostMountPathStdio))
                    {
                        return $"Translation complete!\nOutput file: {outputPath}\n\nNote: The file is in the container's /files folder.";
                    }
                    
                    string hostOutputFileStdio = Path.Combine(hostMountPathStdio, outputFileName);
                    
                    return $@"Translation complete!

Output file is ready at:
{hostOutputFileStdio}

If you want to copy the file to a different location, you can use:
cp ""{hostOutputFileStdio}"" ""/path/to/destination/{outputFileName}""";

                case ExecutionMode.HttpContainer:
                    // HTTP Container 모드: 마운트 폴더 경로 확인
                    string? hostMountPathHttp = ExecutionModeDetector.GetHostMountPath();
                    
                    if (string.IsNullOrEmpty(hostMountPathHttp))
                    {
                        return $@"Translation complete!

Download your file:
http://localhost:8080/download/{outputFileName}

Or use curl:
curl -o ""{outputFileName}"" http://localhost:8080/download/{outputFileName}";
                    }
                    
                    string hostOutputFileHttp = Path.Combine(hostMountPathHttp, outputFileName);
                    
                    return $@"Translation complete!

Download your file:
http://localhost:8080/download/{outputFileName}

Or use curl:
curl -o ""{outputFileName}"" http://localhost:8080/download/{outputFileName}

File is also available at: {hostOutputFileHttp}";

                case ExecutionMode.HttpRemote:
                    // Azure 모드: Container App의 FQDN 사용
                    // Azure Container Apps는 자동으로 CONTAINER_APP_HOSTNAME 환경 변수 제공
                    string? containerAppHostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
                    
                    string downloadUrl;
                    if (!string.IsNullOrEmpty(containerAppHostname))
                    {
                        downloadUrl = $"https://{containerAppHostname}/download/{outputFileName}";
                    }
                    else
                    {
                        // Fallback: localhost (로컬 테스트용)
                        downloadUrl = $"http://localhost:8080/download/{outputFileName}";
                    }
                    
                    return $@"Translation complete!

Download your file:
{downloadUrl}

Or use curl:
curl -o ""{outputFileName}"" {downloadUrl}";

                default:
                    return $"Translation complete!\nOutput: {outputPath}";
            }
        }
    }
}
