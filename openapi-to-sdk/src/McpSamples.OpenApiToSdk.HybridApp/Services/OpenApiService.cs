using System.Diagnostics;
using System.IO.Compression;
using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

public class OpenApiService(OpenApiToSdkAppSettings settings, IHttpContextAccessor httpContextAccessor, ILogger<OpenApiService> logger) : IOpenApiService
{
    public async Task<string> GenerateSdkAsync(string specSource, string language, string? clientClassName, string? namespaceName, string? additionalOptions, CancellationToken cancellationToken = default)
    {
        // 0. 기본값 설정 및 옵션 처리
        if (string.IsNullOrWhiteSpace(specSource))
            throw new ArgumentException("Spec source cannot be empty.", nameof(specSource));
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
                // 컨테이너/Azure 환경에서는 마운트된 볼륨의 specs 폴더를 확인합니다.
                // 리눅스 컨테이너에서 윈도우 경로(\)가 들어올 경우를 대비해 파일명만 확실하게 추출합니다.
                string fileName = Path.GetFileName(specSource);
                if (fileName.Contains('\\'))
                {
                    fileName = fileName.Split('\\').Last();
                }

                inputPath = Path.Combine(settings.SpecsPath, fileName);

                // 2. 파일이 마운트된 경로에 없으면 에이전트에게 복사를 요청합니다. (에러 처리 X)
                if (!File.Exists(inputPath))
                {
                    if (settings.IsAzure) // [수정] Azure 환경 처리: HTTP 업로드 안내
                    {
                        // 현재 서버의 업로드 URL 계산
                        string uploadUrl = "/upload"; // Fallback
                        var request = httpContextAccessor.HttpContext?.Request;
                        if (request != null)
                        {
                            // 예: https://myapp.azurecontainerapps.io/upload
                            uploadUrl = $"{request.Scheme}://{request.Host}/upload";
                        }

                        // Agent에게 'curl' 명령어를 실행하라고 지시
                        return $"""
                        ⚠️ Action Required: File Upload Needed (Azure)

                        I am running in an Azure environment and cannot access your local file `{specSource}` directly.
                        To proceed, you need to upload the file to the server.

                        Please execute the appropriate command for your operating system (OS) using your terminal or shell tool:

                        ### 🍎/🐧 macOS & Linux

                        curl -F "file=@{specSource}" {uploadUrl}

                        ### 💻 Windows (PowerShell/CMD)

                        curl.exe -F "file=@{specSource}" {uploadUrl}

                        After the command completes successfully, please call this `generate_sdk` tool again.
                        """;
                    }
                    if (settings.IsContainer)
                    {
                        // 기본 안내 경로 (환경변수 누락 시 대비)
                        string targetHostPath = $"workspace/specs/{fileName}";

                        // 도커 실행 시 주입된 HOST_ROOT_PATH 환경 변수 확인
                        // 옵션: -e HOST_ROOT_PATH=${env:REPOSITORY_ROOT}/openapi-to-sdk
                        string? hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");

                        if (!string.IsNullOrEmpty(hostRootPath))
                        {
                            // 1. 경로 정규화: 윈도우(\)와 리눅스(/) 구분자를 모두 포워드 슬래시('/')로 통일
                            string normalizedHostRoot = hostRootPath.Replace('\\', '/').TrimEnd('/');

                            // 2. 최종 호스트 타겟 경로 조립
                            // 예: D:/Projects/openapi-to-sdk/workspace/specs/petstore.json
                            targetHostPath = $"{normalizedHostRoot}/workspace/specs/{fileName}";
                        }

                        // 에러가 아닌 '조치 요청' 메시지 반환
                        return $"""
                        ⚠️ Action Required: File Synchronization Needed

                        The file '{fileName}' is not currently in the shared volume visible to the container.
                        To proceed with SDK generation, I need access to this file.

                        Please perform the following steps using your filesystem tools:
                        1. COPY the file from: `{specSource}`
                        2. PASTE it to: `{targetHostPath}`

                        After copying the file, please call this 'generate_sdk' tool again with the same arguments.
                        """;
                    }
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
            string relativePath = $"/download/{zipFileName}";
            string downloadUrl;

            // 현재 요청(Request) 정보 가져오기
            var request = httpContextAccessor.HttpContext?.Request;

            if (request != null)
            {
                // Local, Docker, Azure 모두 현재 접속된 Host(도메인+포트)를 기준으로 URL 생성
                // 예: http://localhost:5222, https://myapp.azurecontainerapps.io 등
                string baseUrl = $"{request.Scheme}://{request.Host}";
                downloadUrl = $"{baseUrl}{relativePath}";
            }
            else
            {
                // HttpContext가 없는 예외적인 경우 (Fallback)
                // Azure나 Docker는 보통 포트 8080, 로컬은 5222 등 다양하므로 상대 경로만 제공
                downloadUrl = relativePath;
            }

            return $"✅ SDK Generation Successful!\n\n" +
                   $"Download Link: {downloadUrl}";
        }
        else
        {
            string finalPath = localZipPath;

            if (settings.IsContainer) // Docker Stdio 모드
            {
                // 호스트 경로 환경 변수 읽기
                // 예: "D:/KNU/3-2/CDP1/openapi-to-sdk"
                string? hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");

                if (!string.IsNullOrEmpty(hostRootPath))
                {
                    // 1. 컨테이너 경로의 시작인 /app을 제외합니다.
                    // finalPath = /app/workspace/...
                    string relativePathFromApp = finalPath.Substring("/app".Length).TrimStart('/');

                    // 2. 호스트 경로의 끝에 있는 슬래시(/)나 역슬래시(\)를 정리합니다.
                    string hostPathNormalized = hostRootPath.TrimEnd('/', '\\');

                    // 3. 크로스 플랫폼 호환성을 위해 최종 경로를 포워드 슬래시(/)로 연결합니다.
                    // Path.Combine 대신 string concatenation을 사용하여 OS 종속성을 제거합니다.
                    finalPath = $"{hostPathNormalized}/{relativePathFromApp}";
                }
            }
            // Stdio 모드 (기존 동일)
            return $"✅ SDK Generation Successful!\n\n" +
                   $"File Saved At: {localZipPath}\n\n" +
                   $"The file is currently in the workspace. Please check if this location is correct.\n" +
                   $"If the user wants the file elsewhere, please move it to the desired destination.";
        }
    }
}