using System.ComponentModel;
using McpSamples.OpenApiToSdk.HybridApp.Models;
using McpSamples.OpenApiToSdk.HybridApp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Tools;

/// <summary>
/// Represents the tool for generating an SDK from an OpenAPI specification.
/// </summary>
/// <param name="openApiService">The service for handling OpenAPI operations.</param>
/// <param name="logger">The logger for this tool.</param>
[McpServerToolType]
public class OpenApiToSdkTool(IOpenApiService openApiService, ILogger<OpenApiToSdkTool> logger)
{
    /// <summary>
    /// Generates a client SDK from an OpenAPI specification provided as a URL or raw content.
    /// </summary>
    /// <param name="specSource">The URL or raw text content of the OpenAPI specification (JSON/YAML).</param>
    /// <param name="language">The target language for the SDK (e.g., CSharp, Java, Python).</param>
    /// <param name="className">The name for the client class (optional).</param>
    /// <param name="namespaceName">The namespace for the generated client code (optional).</param>
    /// <param name="additionalOptions">Additional command-line options to pass to the Kiota tool (optional).</param>
    /// <returns>An <see cref="OpenApiToSdkResult"/> containing the result of the generation process.</returns>
    [McpServerTool(Name = "generate_sdk", Title = "Generate SDK from OpenAPI")]
    [Description("Generates a client SDK. Accepts either a URL or raw OpenAPI Content (JSON/YAML).")]
    public async Task<OpenApiToSdkResult> GenerateSdkAsync(
        [Description("The OpenAPI source. Provide a URL (http://...) OR the raw content text (JSON/YAML).")] string specSource,
        [Description("The target language (e.g., CSharp, Java, Python).")] string language,
        [Description("The class name for the client (optional).")] string? className = null,
        [Description("The namespace for the client (optional).")] string? namespaceName = null,
        [Description("Additional Kiota CLI options (optional).")] string? additionalOptions = null)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(specSource))
        {
            return new OpenApiToSdkResult
            {
                ErrorMessage = "The 'specSource' parameter is required. It must be a URL or OpenAPI content."
            };
        }

        logger.LogInformation("Generating SDK for language: {Language}", language);

        try
        {
            // Call the service to perform the generation
            return await openApiService.GenerateSdkAsync(
                specSource,
                language,
                className,
                namespaceName,
                additionalOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute generate_sdk tool.");
            return new OpenApiToSdkResult { ErrorMessage = $"Tool execution error: {ex.Message}" };
        }
    }
}