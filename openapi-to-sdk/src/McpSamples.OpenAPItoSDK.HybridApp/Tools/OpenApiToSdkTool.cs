using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using McpSamples.OpenApiToSdk.HybridApp.Models;
using McpSamples.OpenApiToSdk.HybridApp.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    /// <returns>A <see cref="DownloadResult"/> containing the downloaded content or an error message.</returns>
    Task<DownloadResult> DownloadOpenApiSpecAsync(string openApiUrl);

    /// <summary>
    /// Generates an SDK from an OpenAPI specification using Kiota.
    /// </summary>
    /// <param name="openApiUrl">The URL of the OpenAPI specification.</param>
    /// <param name="language">The target language for the SDK (e.g., "csharp", "typescript").</param>
    /// <param name="additionalOptions">Additional Kiota CLI options.</param>
    /// <returns>An <see cref="OpenApiToSdkResult"/> containing the path to the generated SDK ZIP file or an error message.</returns>
    Task<OpenApiToSdkResult> GenerateSdkAsync(string openApiUrl, string language, string? additionalOptions = null);
}

/// <summary>
/// This represents the tool entity for OpenAPI to SDK operations.
/// </summary>
[McpServerToolType]
public class OpenApiToSdkTool : IOpenApiToSdkTool
{
    private readonly IOpenApiService _openApiService;
    private readonly ILogger<OpenApiToSdkTool> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OpenApiToSdkTool(IOpenApiService openApiService, ILogger<OpenApiToSdkTool> logger, IHostEnvironment hostEnvironment, IHttpContextAccessor httpContextAccessor)
    {
        _openApiService = openApiService ?? throw new ArgumentNullException(nameof(openApiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    [McpServerTool(Name = "download_openapi_spec")]
    [Description("Download OpenAPI specification from a URL")]
    public async Task<DownloadResult> DownloadOpenApiSpecAsync(
        [Description("URL of the OpenAPI specification")] string openApiUrl)
    {
        var result = new DownloadResult();
        try
        {
            result.Content = await _openApiService.DownloadOpenApiSpecAsync(openApiUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading OpenAPI spec from {Url}", openApiUrl);
            result.ErrorMessage = ex.Message;
        }
        return result;
    }

    /// <inheritdoc />
    [McpServerTool(Name = "generate_sdk")]
    [Description("Generate an SDK from an OpenAPI specification using Kiota")]
    public async Task<OpenApiToSdkResult> GenerateSdkAsync(
        [Description("URL of the OpenAPI specification")] string openApiUrl,
        [Description("Target language for the SDK (e.g., csharp, typescript)")] string language,
        [Description("Optional extra Kiota options (e.g., --namespace Contoso.Api)")] string? additionalOptions = null)
    {
        var result = new OpenApiToSdkResult();
        var tempSpecPath = string.Empty;
        var sdkOutputDir = string.Empty;
        var tempZipPath = string.Empty;

        try
        {
            var specContent = await _openApiService.DownloadOpenApiSpecAsync(openApiUrl);
            if (string.IsNullOrWhiteSpace(specContent))
            {
                result.ErrorMessage = "Failed to download or empty OpenAPI specification.";
                return result;
            }

            var tempDir = Path.GetTempPath();
            tempSpecPath = Path.Combine(tempDir, $"openapi-spec-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempSpecPath, specContent);

            sdkOutputDir = Path.Combine(tempDir, $"sdk-output-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sdkOutputDir);

            var error = await _openApiService.RunKiotaAsync(tempSpecPath, language, sdkOutputDir, additionalOptions);
            if (!string.IsNullOrEmpty(error))
            {
                result.ErrorMessage = $"SDK generation failed: {error}";
                return result;
            }

            // Prepare public directory for the zip file
            var downloadsDir = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot", "generated-sdks");
            Directory.CreateDirectory(downloadsDir);
            var zipFileName = $"{language}-{DateTime.Now:yyyyMMddHHmmss}.zip"; // Changed to language-datetime.zip
            var finalZipPath = Path.Combine(downloadsDir, zipFileName);

            // Compress generated SDK to ZIP
            ZipFile.CreateFromDirectory(sdkOutputDir, finalZipPath);

            // Construct downloadable URI
            if (_httpContextAccessor.HttpContext is not null)
            {
                var request = _httpContextAccessor.HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                result.ZipPath = $"{baseUrl}/generated-sdks/{zipFileName}";
            }
            else
            {
                // Fallback to local file path for non-HTTP contexts (e.g., STDIO)
                result.ZipPath = finalZipPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred during SDK generation.");
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            // Cleanup temporary files and directories
            if (!string.IsNullOrEmpty(tempSpecPath) && File.Exists(tempSpecPath))
            {
                File.Delete(tempSpecPath);
            }
            if (!string.IsNullOrEmpty(sdkOutputDir) && Directory.Exists(sdkOutputDir))
            {
                Directory.Delete(sdkOutputDir, true);
            }
            // The finalZipPath is in wwwroot, so it's not cleaned up here.
            // It's meant to be served.
        }

        return result;
    }
}