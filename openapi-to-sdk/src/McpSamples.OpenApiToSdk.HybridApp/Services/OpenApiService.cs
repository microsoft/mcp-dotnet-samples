using System.Diagnostics;
using System.IO.Compression;
using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using Microsoft.Extensions.Logging;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

public class OpenApiService(OpenApiToSdkAppSettings settings, ILogger<OpenApiService> logger) : IOpenApiService
{
    public async Task<string> GenerateSdkAsync(string specSource, string language, string? clientClassName, string? namespaceName, string? additionalOptions, CancellationToken cancellationToken = default)
    {
        // 0. 기본값 설정 및 옵션 처리
        var finalClassName = string.IsNullOrWhiteSpace(clientClassName) ? "ApiClient" : clientClassName;
        var finalNamespace = string.IsNullOrWhiteSpace(namespaceName) ? "ApiSdk" : namespaceName;
        var finalOptions = additionalOptions ?? string.Empty;

        // 1. 입력 소스 판별 (URL vs 파일 경로)
        string inputPath;
        bool isUrl = Uri.TryCreate(specSource, UriKind.Absolute, out var uriResult)
                     && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        if (isUrl)
        {
            inputPath = specSource;
            logger.LogInformation("Input is a URL: {InputPath}", inputPath);
        }
        else
        {
            if (settings.IsContainer || settings.IsAzure)
            {
                string fileName = Path.GetFileName(specSource);
                inputPath = Path.Combine(settings.SpecsPath, fileName);

                if (!File.Exists(inputPath))
                {
                    var errorMsg = $"[Error] File not found in mounted volume: {inputPath}.\n" +
                                   $"For Docker/Azure: Please ensure the spec file is uploaded/copied to the mounted 'workspace/specs' folder.";
                    logger.LogError(errorMsg);
                    return errorMsg;
                }
            }
            else
            {
                inputPath = specSource;
                if (!File.Exists(inputPath))
                {
                    var errorMsg = $"[Error] Local file not found: {inputPath}";
                    logger.LogError(errorMsg);
                    return errorMsg;
                }
            }
            logger.LogInformation("Input is a File: {InputPath}", inputPath);
        }

        // 2. 임시 출력 폴더 생성
        string outputId = Guid.NewGuid().ToString();
        string tempOutputPath = Path.Combine(settings.GeneratedPath, outputId);
        Directory.CreateDirectory(tempOutputPath);

        try
        {
            // 3. Kiota 실행
            // additionalOptions가 포함된 Arguments 구성
            logger.LogInformation("Starting Kiota generation...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "kiota",
                Arguments = $"generate -l {language} -c {finalClassName} -n {finalNamespace} -d \"{inputPath}\" -o \"{tempOutputPath}\" {finalOptions}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(cancellationToken);
                logger.LogError("Kiota generation failed: {Error}", error);
                return $"[Error] Kiota generation failed:\n{error}";
            }

            // 4. Zip 압축
            string zipFileName = $"sdk-{language}-{outputId.Substring(0, 4)}.zip";
            string zipFilePath = Path.Combine(settings.GeneratedPath, zipFileName);

            ZipFile.CreateFromDirectory(tempOutputPath, zipFilePath);
            logger.LogInformation("SDK generated and zipped at: {ZipFilePath}", zipFilePath);

            // 5. 결과 반환 메시지 생성
            return CreateResultMessage(zipFileName, zipFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during SDK generation.");
            return $"[Error] An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            if (Directory.Exists(tempOutputPath))
            {
                try
                {
                    Directory.Delete(tempOutputPath, true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to clean up temp directory: {TempPath}", tempOutputPath);
                }
            }
        }
    }

    private string CreateResultMessage(string zipFileName, string localZipPath)
    {
        if (settings.IsHttpMode)
        {
            string downloadUrl = $"/download/{zipFileName}";
            return $"✅ SDK Generation Successful!\n\n" +
                   $"Download Link: {downloadUrl}\n" +
                   $"(Note: If accessing locally via browser, prepend your host address, e.g., http://localhost:8080{downloadUrl})";
        }
        else
        {
            return $"✅ SDK Generation Successful!\n\n" +
                   $"File Saved At: {localZipPath}\n\n" +
                   $"The file is currently in the workspace. Please check if this location is correct.\n" +
                   $"If the user wants the file elsewhere, please move it to the desired destination.";
        }
    }
}