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
            if (settings.Runtime.Mode == "Local")
            {
                inputPath = specSource;

                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException($"Local file not found at: {inputPath}. Please check the path.");
                }
            }
            else
            {
                string fileName = Path.GetFileName(specSource);
                if (fileName.Contains('\\'))
                {
                    fileName = fileName.Split('\\').Last();
                }

                inputPath = Path.Combine(settings.SpecsPath, fileName);

                if (!File.Exists(inputPath))
                {
                    switch (settings.Runtime.Mode)
                    {
                        case "Azure":
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
                            Invoke-RestMethod -Uri "{{uploadUrl}}" -Method Post -Form @{ file = Get-Item -Path "{{specSource}}" }

                            After the command completes successfully, please call this `generate_sdk` tool again.
                            """;
                        case "Container":
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

                        default:
                            throw new FileNotFoundException($"File not found in volume: {inputPath}");
                    }
                }
            }
        }

        var outputId = Guid.NewGuid().ToString();
        var tempOutputPath = Path.Combine(settings.GeneratedPath, outputId);

        if (!Directory.Exists(tempOutputPath))
        {
            Directory.CreateDirectory(tempOutputPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "kiota",
            Arguments = $"generate -l {language} -c {finalClassName} -n {finalNamespace} -d \"{inputPath}\" -o \"{tempOutputPath}\" {finalOptions}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Kiota process.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            logger.LogError("Kiota failed: {StdErr}", stderr);
            return $"[Error] Kiota generation failed:\n{stderr}\n{stdout}";
        }

        string zipFileName = $"sdk-{language}-{outputId.Substring(0, 8)}.zip";
        string localZipPath = Path.Combine(settings.GeneratedPath, zipFileName);

        ZipFile.CreateFromDirectory(tempOutputPath, localZipPath);

        try
        {
            Directory.Delete(tempOutputPath, true);
        }
        catch
        {
        }

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

            if (settings.Runtime.Mode == "Container")
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