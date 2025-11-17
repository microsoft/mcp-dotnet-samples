using System.Diagnostics;
using System.Text;

namespace McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// This represents the service for OpenAPI operations.
/// </summary>
/// <param name="httpClient">The <see cref="HttpClient"/> instance.</param>
/// <param name="logger">The <see cref="ILogger{OpenApiService}"/> instance.</param>
public class OpenApiService(HttpClient httpClient, ILogger<OpenApiService> logger) : IOpenApiService
{
    /// <inheritdoc />
    public async Task<string> DownloadOpenApiSpecAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        logger.LogInformation("Downloading OpenAPI spec from {Url}", url);
        var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Downloaded OpenAPI spec from {Url} (Length={Length})", url, content.Length);

        return content;
    }

    /// <inheritdoc />
    public async Task<string?> RunKiotaAsync(string openApiSpecPath, string language, string outputDir, string? additionalOptions = null)
    {
        // Map Kiota command options
        var arguments = new StringBuilder();
        arguments.Append($"generate");
        arguments.Append($" --openapi \"{openApiSpecPath}\" --language {language} --output \"{outputDir}\"");
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
            if (process is null)
            {
                return "Failed to start Kiota process.";
            }

            // Execute with timeout (5 minutes)
            var timeout = TimeSpan.FromMinutes(5);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();

            if (await Task.WhenAny(exitTask, Task.Delay(timeout)).ConfigureAwait(false) == exitTask)
            {
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    logger.LogError("Kiota execution failed (ExitCode={ExitCode}): {Error}", process.ExitCode, await errorTask);
                    return $"Kiota error: {await errorTask}";
                }

                logger.LogInformation("Kiota execution succeeded: {Output}", await outputTask);
                return null;
            }
            else
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cleanup
                }

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