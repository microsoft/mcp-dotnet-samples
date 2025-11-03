using System.ComponentModel;
using System.IO;

using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.OpenApiToSdk.HybridApp.Services;

using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the OpenAPI to SDK tool.
/// </summary>
public interface IOpenApiToSdkTool
{
    /// <summary>
    /// Downloads OpenAPI specification from a URL.
    /// </summary>
    /// <param name="openApiUrl">The URL of the OpenAPI specification.</param>
    /// <returns>The downloaded OpenAPI specification content.</returns>
    Task<string> DownloadOpenApiSpecAsync(string openApiUrl);

    /// <summary>
    /// Generates an SDK from an OpenAPI specification using Kiota.
    /// </summary>
    /// <param name="openApiUrl">The URL of the OpenAPI specification.</param>
    /// <param name="language">The target language for the SDK (e.g., "csharp", "typescript").</param>
    /// <param name="outputDir">Optional output directory for the generated SDK.</param>
    /// <returns>The path to the generated SDK ZIP file or an error message.</returns>
    Task<string> GenerateSdkAsync(string openApiUrl, string language, string? outputDir = null);
}

/// <summary>
/// This represents the tool entity for OpenAPI to SDK operations.
/// </summary>
/// <param name="openApiService"><see cref="IOpenApiService"/> instance.</param>
public class OpenApiToSdkTool(IOpenApiService openApiService) : IOpenApiToSdkTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "download_openapi_spec")]
    [Description("Download OpenAPI specification from a URL")]
    public async Task<string> DownloadOpenApiSpecAsync(
        [Description("URL of the OpenAPI specification")] string openApiUrl)
    {
        return await openApiService.DownloadOpenApiSpecAsync(openApiUrl);
    }

    /// <inheritdoc />
    [McpServerTool(Name = "generate_sdk")]
    [Description("Generate an SDK from an OpenAPI specification using Kiota")]
    public async Task<string> GenerateSdkAsync(
        [Description("URL of the OpenAPI specification")] string openApiUrl,
        [Description("Target language for the SDK (e.g., csharp, typescript)")] string language,
        [Description("Optional output directory for the generated SDK")] string? outputDir = null)
    {
        // 1. Download OpenAPI spec
        var specContent = await openApiService.DownloadOpenApiSpecAsync(openApiUrl);
        if (string.IsNullOrEmpty(specContent))
        {
            return "Failed to download OpenAPI specification.";
        }

        // 2. Create project temp directory (openapi-to-sdk/temp/)
        var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        var projectTempDir = Path.Combine(projectRoot, "temp");
        Directory.CreateDirectory(projectTempDir);

        // 3. Save spec to temp file (Kiota requires file path)
        var tempSpecPath = Path.Combine(projectTempDir, $"openapi-spec-{Guid.NewGuid()}.json");
        Console.WriteLine($"Temporary OpenAPI spec path: {tempSpecPath}");
        await File.WriteAllTextAsync(tempSpecPath, specContent);

        // 4. Set output directory (default: temp folder)
        var finalOutputDir = outputDir ?? Path.Combine(projectTempDir, $"sdk-output-{Guid.NewGuid()}");
        Directory.CreateDirectory(finalOutputDir);

        // 4. Map Kiota command options
        var additionalOptions = "";  // Additional options (e.g., --namespace MyNamespace) can be extended
        var error = await openApiService.RunKiotaAsync(tempSpecPath, language, finalOutputDir, additionalOptions);

        // 5. Clean up temp file
        File.Delete(tempSpecPath);

        if (!string.IsNullOrEmpty(error))
        {
            return $"SDK generation failed: {error}";
        }

        // 6. Compress generated SDK to ZIP (refer to next task)
        var zipPath = Path.Combine(projectTempDir, $"sdk-{Guid.NewGuid()}.zip");
        // TODO: Implement ZIP compression logic (add separate service)

        return $"SDK generated successfully. ZIP path: {zipPath}";  // Eventually return URI (Feature 1.2)
    }
}