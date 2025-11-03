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
    /// <param name="additionalOptions">Additional user-provided options for Kiota.</param>
    /// <returns>A formatted prompt for SDK generation with parsing instructions.</returns>
    string GetSdkGenerationPrompt(string openApiDocUrl, string language, string? additionalOptions = null);

    /// <summary>
    /// Gets a prompt for validating an OpenAPI specification.
    /// </summary>
    /// <param name="openApiDocUrl">The URL of the OpenAPI specification.</param>
    /// <returns>A formatted prompt for validating the OpenAPI spec.</returns>
    string GetValidationPrompt(string openApiDocUrl);
}

/// <summary>
/// This provides prompts for SDK generation from OpenAPI specs.
/// </summary>
[McpServerPromptType]
public class SdkGenerationPrompt : ISdkGenerationPrompt
{
    /// <inheritdoc />
    [McpServerPrompt(Name = "generate_sdk_with_parsing", Title = "Generate SDK from OpenAPI Spec with Kiota Parsing")]
    [Description("Provides a structured prompt for parsing Kiota options and generating an SDK.")]
    public string GetSdkGenerationPrompt(
        [Description("The Location of the OpenAPI description.")] string openApiDocUrl,
        [Description("The target language for the SDK.")] string language,
        [Description("Additional options for Kiota (e.g., '--namespace-name MyNamespace').")] string? additionalOptions = null)
    {
        return $"""
            Generate an SDK from the provided OpenAPI specification using Kiota.

            OpenAPI Location: {openApiDocUrl}
            Language: {language}
            Additional Options: {additionalOptions ?? "None"}

            Instructions:
            - Parse the additional options to valid Kiota command-line arguments.
            - Use the 'generate_sdk' tool with the parsed options to process the spec.
            - Validate the OpenAPI spec before generation.
            - Return the ZIP file URI upon success.
            - Handle errors gracefully and provide feedback.
            """;
    }

    /// <inheritdoc />
    [McpServerPrompt(Name = "validate_openapi_spec", Title = "Validate OpenAPI Specification")]
    [Description("Provides a structured prompt for validating an OpenAPI specification before SDK generation.")]
    public string GetValidationPrompt(
        [Description("The OpenAPI specification URL.")] string openApiDocUrl)
    {
        return $"""
            Validate the provided OpenAPI specification.

            OpenAPI Location: {openApiDocUrl}

            Instructions:
            - Download and parse the OpenAPI spec.
            - Check for syntax errors, missing required fields, and schema validity.
            - Report any validation issues or confirm validity.
            - Use this validation before proceeding to SDK generation.
            """;
    }
}