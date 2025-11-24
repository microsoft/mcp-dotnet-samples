using System.ComponentModel;

using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for SDK generation prompts.
/// </summary>
public interface ISdkGenerationPrompt
{
    /// <summary>
    /// Gets a prompt for generating an SDK from an OpenAPI specification, including parsing Kiota options.
    /// </summary>
    /// <param name="openApiDocUrl">The location of the OpenAPI description.</param>
    /// <param name="language">The target language for the SDK.</param>
    /// <param name="className">Optional: The class name to use for the core client class.</param>
    /// <param name="namespaceName">Optional: The namespace to use for the core client class.</param>
    /// <param name="additionalOptions">Additional user-provided options for Kiota.</param>
    /// <returns>A formatted prompt for SDK generation with parsing instructions.</returns>
    string GetSdkGenerationPrompt(string openApiDocUrl, string language, string? className = null, string? namespaceName = null, string? additionalOptions = null);
}

/// <summary>
/// This provides prompts for SDK generation from OpenAPI specs.
/// </summary>
[McpServerPromptType]
public class SdkGenerationPrompt : ISdkGenerationPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "generate_sdk", Title = "Generate SDK from OpenAPI Spec with Kiota Parsing")]
    [Description("Provides a structured prompt for parsing Kiota options and generating an SDK.")]
    public string GetSdkGenerationPrompt(
        [Description("The URL or local file path of the OpenAPI description.")] string openApiDocUrl,
        [Description("The target language for the SDK.")] string language,
        [Description("The class name to use for the core client class. Defaults to ApiClient.")] string? className = null,
        [Description("The namespace to use for the core client class. Defaults to ApiSdk.")] string? namespaceName = null,
        [Description("Additional options for Kiota (e.g., '--include-path Paths').")] string? additionalOptions = null)
    {
        return $"""
            Generate an SDK from the provided OpenAPI specification using Kiota.

            OpenAPI Source: {openApiDocUrl}
            Language: {language}
            Class Name: {className ?? "Default (ApiClient)"}
            Namespace: {namespaceName ?? "Default (ApiSdk)"}
            Additional Options: {additionalOptions ?? "None"}

            Instructions:
            - Parse the options to valid Kiota command-line arguments.
            - Use the 'generate_sdk' tool with the parsed options to process the spec.
            - Validate the OpenAPI spec before generation.
            - Return the ZIP file URI upon success.
            - Handle errors gracefully and provide feedback.
            """;
    }
}