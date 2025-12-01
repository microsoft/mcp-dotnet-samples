using System.Diagnostics;
using System.IO.Compression;
using McpSamples.OpenApiToSdk.HybridApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This represents the service entity for OpenAPI operations. It handles downloading OpenAPI specifications,
/// generating SDKs using Kiota, and managing temporary files.
/// </summary>
/// <param name="httpClient">An <see cref="HttpClient"/> instance for making HTTP requests.</param>
/// <param name="logger">An <see cref="ILogger{TCategoryName}"/> instance for logging.</param>
/// <param name="httpContextAccessor">An <see cref="IHttpContextAccessor"/> to access the current HTTP context (optional).</param>
public class OpenApiService(
    HttpClient httpClient,
    ILogger<OpenApiService> logger,
    IHttpContextAccessor? httpContextAccessor = null) : IOpenApiService
{
    /// <inheritdoc />
    public async Task<OpenApiToSdkResult> GenerateSdkAsync(
            string specSource,
            string language,
            string? className = null,
            string? namespaceName = null,
            string? additionalOptions = null)
    {
        CleanupOldFiles();

        var result = new OpenApiToSdkResult();
        string kiotaInputPath;
        string? tempInputFile = null;
        var tempGenDir = Path.Combine(Path.GetTempPath(), "kiota_gen_" + Guid.NewGuid());

        try
        {
            // Auto-detects if the source is a URL or raw content.
            if (IsUrl(specSource))
            {
                // If it's a URL, pass it directly to Kiota.
                kiotaInputPath = specSource;
                logger.LogInformation("Detected URL input: {Url}", specSource);
            }
            else
            {
                // If it's raw content, save it to a temporary file and pass the path.
                tempInputFile = await CreateTempFileFromContentAsync(specSource);
                kiotaInputPath = tempInputFile;
                logger.LogInformation("Detected raw content input. Created temp file: {Path}", tempInputFile);
            }

            var optionsList = new List<string>();
            if (!string.IsNullOrWhiteSpace(className))
            {
                optionsList.Add($"--class-name \"{className}\"");
            }
            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                optionsList.Add($"--namespace-name \"{namespaceName}\"");
            }
            if (!string.IsNullOrWhiteSpace(additionalOptions))
            {
                optionsList.Add(additionalOptions);
            }

            var combinedOptions = string.Join(" ", optionsList);

            Directory.CreateDirectory(tempGenDir);
            var kiotaError = await RunKiotaAsync(kiotaInputPath, language, tempGenDir, combinedOptions);

            if (!string.IsNullOrEmpty(kiotaError))
            {
                result.ErrorMessage = kiotaError;
                return result;
            }

            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var outputDir = Path.Combine(webRootPath, "generated");
            Directory.CreateDirectory(outputDir);

            var fileName = $"{language}-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}.zip";
            var finalZipPath = Path.Combine(outputDir, fileName);

            ZipFile.CreateFromDirectory(tempGenDir, finalZipPath);

            result.ServerFilePath = finalZipPath;
            var request = httpContextAccessor?.HttpContext?.Request;

            if (request != null)
            {
                var baseUrl = $"{request.Scheme}://{request.Host}";
                result.ZipPath = $"{baseUrl}/generated/{fileName}";
                result.Message = $"SDK generation successful. Download link: {result.ZipPath}";
            }
            else
            {
                var fileUri = "file:///" + finalZipPath.Replace("\\", "/").TrimStart('/');
                result.ZipPath = fileUri;
                result.Message = $"SDK generation successful! File Location: {fileUri}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during SDK generation workflow.");
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            CleanupTempResources(tempInputFile, tempGenDir);
        }

        return result;
    }

    /// <summary>
    /// Determines if the given string is an absolute URL.
    /// </summary>
    /// <param name="input">The string to check.</param>
    /// <returns><c>true</c> if the input is a valid HTTP or HTTPS URL; otherwise, <c>false</c>.</returns>
    private bool IsUrl(string input)
    {
        return Uri.TryCreate(input, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Deletes old generated SDK files from the output directory to save space.
    /// </summary>
    private void CleanupOldFiles()
    {
        try
        {
            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var outputDir = Path.Combine(webRootPath, "generated");

            if (Directory.Exists(outputDir))
            {
                var files = Directory.GetFiles(outputDir);
                foreach (var file in files)
                {
                    if (DateTime.Now - File.GetCreationTime(file) > TimeSpan.FromHours(1))
                    {
                        try { File.Delete(file); }
                        catch
                        {
                            // Ignored
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to clean up old generated files: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Cleans up temporary files and directories created during SDK generation.
    /// </summary>
    /// <param name="tempFile">The path to the temporary input file to delete.</param>
    /// <param name="tempDir">The path to the temporary generation directory to delete.</param>
    private void CleanupTempResources(string? tempFile, string tempDir)
    {
        if (tempFile != null && File.Exists(tempFile))
        {
            try { File.Delete(tempFile); }
            catch (Exception ex) { logger.LogWarning("Failed to delete temp file: {Message}", ex.Message); }
        }

        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); }
            catch (Exception ex) { logger.LogWarning("Failed to delete temp dir: {Message}", ex.Message); }
        }
    }

    /// <summary>
    /// Creates a temporary JSON file from a string content.
    /// </summary>
    /// <param name="content">The string content to write to the file.</param>
    /// <returns>The path to the newly created temporary file.</returns>
    private async Task<string> CreateTempFileFromContentAsync(string content)
    {
        var tempPath = Path.GetTempFileName();
        var jsonPath = Path.ChangeExtension(tempPath, ".json");
        if (File.Exists(tempPath))
        {
            File.Move(tempPath, jsonPath, true);
        }
        await File.WriteAllTextAsync(jsonPath, content);
        return jsonPath;
    }

    /// <inheritdoc />
    public async Task<string> DownloadOpenApiSpecAsync(string openApiUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(openApiUrl))
        {
            throw new ArgumentException("URL is required.", nameof(openApiUrl));
        }
        var response = await httpClient.GetAsync(openApiUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> RunKiotaAsync(string openApiSpecPath, string language, string outputDir, string? additionalOptions = null)
    {
        try
        {
            var arguments = $"generate -l {language} -d \"{openApiSpecPath}\" -o \"{outputDir}\" {additionalOptions}";

            var startInfo = new ProcessStartInfo
            {
                FileName = "kiota",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            logger.LogInformation("Running Kiota: kiota {Arguments}", arguments);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var timeout = TimeSpan.FromMinutes(5);
            var exitTask = process.WaitForExitAsync();

            if (await Task.WhenAny(exitTask, Task.Delay(timeout)).ConfigureAwait(false) == exitTask)
            {
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    return $"Kiota error: {error}";
                }
                return null;
            }
            else
            {
                try { process.Kill(entireProcessTree: true); }
                catch
                {
                    // Ignored
                }
                return "Kiota execution timed out.";
            }
        }
        catch (Exception ex)
        {
            return $"Kiota exception: {ex.Message}";
        }
    }
}