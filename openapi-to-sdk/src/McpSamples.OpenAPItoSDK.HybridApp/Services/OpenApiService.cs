using System.Diagnostics;
using System.Text;  // Add this for StringBuilder

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This represents the service for OpenAPI operations.
/// </summary>
public class OpenApiService(HttpClient httpClient, ILogger<OpenApiService> logger) : IOpenApiService
{
    /// <inheritdoc />
    public async Task<string> DownloadOpenApiSpecAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Downloading OpenAPI spec from {Url}", url);
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Successfully downloaded OpenAPI spec ({Length} characters)", content.Length);

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download OpenAPI spec from {Url}", url);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?> RunKiotaAsync(string openApiSpecPath, string language, string outputDir, string? additionalOptions = null)
    {
        // Map Kiota command options
        var arguments = new StringBuilder();
        arguments.Append($"generate --openapi \"{openApiSpecPath}\" --language {language} --output \"{outputDir}\"");
        if (!string.IsNullOrWhiteSpace(additionalOptions))
        {
            arguments.Append($" {additionalOptions}");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "kiota",
            Arguments = arguments.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return "Failed to start Kiota process.";
            }

            // Asynchronous execution with timeout (5 minutes)
            var timeout = TimeSpan.FromMinutes(5);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();

            if (await Task.WhenAny(exitTask, Task.Delay(timeout)) == exitTask)
            {
                await Task.WhenAll(outputTask, errorTask);
                if (process.ExitCode != 0)
                {
                    logger.LogError("Kiota execution failed: {Error}", await errorTask);
                    return $"Kiota error: {await errorTask}";
                }
                logger.LogInformation("Kiota execution succeeded: {Output}", await outputTask);
                return null;  // Success
            }
            else
            {
                process.Kill();
                return "Kiota execution timed out.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during Kiota execution");
            return $"Kiota exception: {ex.Message}";
        }
    }
}