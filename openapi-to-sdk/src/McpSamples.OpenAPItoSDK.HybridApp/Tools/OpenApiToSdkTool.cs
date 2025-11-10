using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

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
    /// <param name="language">The target language for the SDK.</param>
    /// <param name="additionalOptions">Additional Kiota CLI options.</param>
    /// <returns>The path to the generated SDK ZIP file or an error message.</returns>
    Task<string> GenerateSdkAsync(string openApiUrl, string language, string? additionalOptions = null);
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
        [Description("Optional extra Kiota options (e.g., --namespace Contoso.Api)")] string? additionalOptions = null)
    {
        // Download OpenAPI spec from URL
        var specContent = await openApiService.DownloadOpenApiSpecAsync(openApiUrl);
        if (string.IsNullOrWhiteSpace(specContent))
        {
            return "Failed to download OpenAPI specification.";
        }

        // Resolve openapi-to-sdk root directory and create temp folder
        var projectRoot = ResolveOpenApiToSdkRoot(Directory.GetCurrentDirectory());
        var projectTempDir = Path.Combine(projectRoot, "temp");
        Directory.CreateDirectory(projectTempDir);

        // Save spec to temporary file (Kiota requires file path)
        var tempSpecPath = Path.Combine(projectTempDir, $"openapi-spec-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempSpecPath, specContent);

        // Create output directory for generated SDK
        var finalOutputDir = Path.Combine(projectTempDir, $"sdk-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(finalOutputDir);

        // Execute Kiota with mapped options
        var error = await openApiService.RunKiotaAsync(tempSpecPath, language, finalOutputDir, additionalOptions);

        // Clean up temporary spec file
        try
        {
            File.Delete(tempSpecPath);
        }
        catch
        {
            // Best effort cleanup
        }

        if (!string.IsNullOrEmpty(error))
        {
            return $"SDK generation failed: {error}";
        }

        // Compress generated SDK to ZIP
        var zipPath = Path.Combine(projectTempDir, $"sdk-{Guid.NewGuid():N}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(finalOutputDir, zipPath);

        return $"SDK generated successfully. ZIP path: {zipPath}";
    }

    /// <summary>
    /// Resolves the openapi-to-sdk project root directory.
    /// </summary>
    /// <param name="start">The starting directory path.</param>
    /// <returns>The resolved project root directory path.</returns>
    private static string ResolveOpenApiToSdkRoot(string start)
    {
        var dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(name, "openapi-to-sdk", StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        // Fallback to current directory if root not found
        return start;
    }
}