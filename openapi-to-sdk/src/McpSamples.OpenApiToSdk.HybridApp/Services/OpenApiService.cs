using System.Diagnostics;
using System.IO.Compression;
using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This represents the service for generating client SDKs from OpenAPI specifications.
/// </summary>
/// <param name="settings"><see cref="OpenApiToSdkAppSettings"/> instance.</param>
/// <param name="httpContextAccessor"><see cref="IHttpContextAccessor"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
public class OpenApiService(OpenApiToSdkAppSettings settings, IHttpContextAccessor httpContextAccessor, ILogger<OpenApiService> logger) : IOpenApiService
{
    /// <inheritdoc />
    public async Task<string> GenerateSdkAsync(string specSource, string language, string? clientClassName, string? namespaceName, string? additionalOptions, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(specSource))
            throw new ArgumentException("Spec source cannot be empty.", nameof(specSource));
        var finalClassName = string.IsNullOrWhiteSpace(clientClassName) ? "ApiClient" : clientClassName;
        var finalNamespace = string.IsNullOrWhiteSpace(namespaceName) ? "ApiSdk" : namespaceName;
        var finalOptions = additionalOptions ?? string.Empty;

        if (finalOptions.Contains("-o ") || finalOptions.Contains("--output "))
        {
            return """
            Input Error: Invalid Option Detected
            
            Please DO NOT include the `-o` or `--output` option in 'additionalOptions'.
            The output path is managed automatically by the server.
            
            If you want to save the file to a specific location, please generate it first, and then move the resulting ZIP file to your desired destination.
            """;
        }

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
                if (fileName.Contains('\\'))
                {
                    fileName = fileName.Split('\\').Last();
                }

                inputPath = Path.Combine(settings.SpecsPath, fileName);

                if (!File.Exists(inputPath))
                {
                    if (settings.IsAzure)
                    {
                        string uploadUrl = "/upload";
                        var request = httpContextAccessor.HttpContext?.Request;
                        if (request != null)
                        {
                            uploadUrl = $"{request.Scheme}://{request.Host}/upload";
                        }

                        return $$"""
                        Action Required: File Upload Needed (Azure)

                        I am running in an Azure environment and cannot access your local file `{{specSource}}` directly.
                        To proceed, you need to upload the file to the server.

                        Please execute the appropriate command for your operating system (OS) using your terminal or shell tool:

                        ### macOS & Linux
                        curl -F "file=@{{specSource}}" {{uploadUrl}}

                        ### Windows (PowerShell)
                        Invoke-RestMethod -Uri "{{uploadUrl}}" -Method Post -Form @{file = Get-Item -Path "{{specSource}}"}

                        After the command completes successfully, please call this `generate_sdk` tool again.
                        """;
                    }
                    if (settings.IsContainer)
                    {
                        string targetHostPath = $"workspace/specs/{fileName}";

                        string? hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");

                        if (!string.IsNullOrEmpty(hostRootPath))
                        {
                            string normalizedHostRoot = hostRootPath.Replace('\\', '/').TrimEnd('/');

                            targetHostPath = $"{normalizedHostRoot}/workspace/specs/{fileName}";
                        }

                        return $"""
                        Action Required: File Synchronization Needed

                        The file '{fileName}' is not currently in the shared volume visible to the container.
                        To proceed with SDK generation, I need access to this file.

                        EXECUTION PLAN (Follow Strictly):
                        1. COPY the file from `{specSource}` to `{targetHostPath}`.
                        2. RETRY the `generate_sdk` tool immediately with the same arguments.
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

        string outputId = Guid.NewGuid().ToString();
        string tempOutputPath = Path.Combine(settings.GeneratedPath, outputId);
        Directory.CreateDirectory(tempOutputPath);

        try
        {
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

            string zipFileName = $"sdk-{language}-{outputId.Substring(0, 4)}.zip";
            string zipFilePath = Path.Combine(settings.GeneratedPath, zipFileName);

            ZipFile.CreateFromDirectory(tempOutputPath, zipFilePath);
            logger.LogInformation("SDK generated and zipped at: {ZipFilePath}", zipFilePath);

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

    /// <summary>
    /// Creates a result message for the SDK generation, including download links for HTTP mode.
    /// </summary>
    /// <param name="zipFileName">The name of the generated ZIP file.</param>
    /// <param name="localZipPath">The local path to the generated ZIP file.</param>
    /// <returns>A formatted string message with download information.</returns>
    private string CreateResultMessage(string zipFileName, string localZipPath)
    {
        if (settings.IsHttpMode)
        {
            string relativePath = $"/download/{zipFileName}";
            string downloadUrl;

            var request = httpContextAccessor.HttpContext?.Request;

            if (request != null)
            {
                string baseUrl = $"{request.Scheme}://{request.Host}";
                downloadUrl = $"{baseUrl}{relativePath}";
            }
            else
            {
                downloadUrl = relativePath;
            }

            return $"SDK Generation Successful!\n" +
                   $"Download Link: {downloadUrl}";
        }
        else
        {
            string finalPath = localZipPath;

            if (settings.IsContainer)
            {
                string? hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");

                if (!string.IsNullOrEmpty(hostRootPath))
                {
                    string relativePathFromApp = finalPath.Substring("/app".Length).TrimStart('/');

                    string hostPathNormalized = hostRootPath.TrimEnd('/', '\\');

                    finalPath = $"{hostPathNormalized}/{relativePathFromApp}";
                }
            }
            return $"SDK Generation Successful!\n" +
                   $"File Saved At: {localZipPath}\n" +
                   $"The file is currently in the workspace. Please check if this location is correct.\n" +
                   $"If the user wants the file elsewhere, please move it to the desired destination.";
        }
    }
}