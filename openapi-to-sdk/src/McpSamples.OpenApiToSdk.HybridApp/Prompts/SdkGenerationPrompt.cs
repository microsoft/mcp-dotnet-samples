using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Prompts;

/// <summary>
/// This provides interfaces for SDK generation prompts.
/// </summary>
public interface ISdkGenerationPrompt
{
  /// <summary>
  /// Gets a structured prompt that guides the LLM to generate an SDK.
  /// </summary>
  /// <param name="language">The target programming language for the SDK.</param>
  /// <param name="openApiSource">The OpenAPI source, which can be a URL, file path, or raw content.</param>
  /// <param name="className">The class name for the core client class (optional).</param>
  /// <param name="namespaceName">The namespace for the core client class (optional).</param>
  /// <param name="additionalOptions">Additional options for Kiota (optional).</param>
  /// <returns>A formatted string representing the SDK generation prompt.</returns>
  string GetSdkGenerationPrompt(
      string language,
      string openApiSource,
      string? className = null,
      string? namespaceName = null,
      string? additionalOptions = null);
}

/// <summary>
/// This represents the prompts entity for SDK generation.
/// </summary>
[McpServerPromptType]
public class SdkGenerationPrompt : ISdkGenerationPrompt
{
  /// <inheritdoc />
  [McpServerPrompt(Name = "generate_sdk", Title = "Generate SDK from OpenAPI Spec")]
  [Description("Returns a structured prompt that guides the LLM to generate an SDK using the 'generate_sdk' tool. It handles language normalization and input source resolution (URL vs File).")]
  public string GetSdkGenerationPrompt(
      [Description("The OpenAPI source. This can be a public URL, a local file path, OR the raw content (JSON/YAML).")] string openApiSource,
      [Description("The target language (e.g. csharp, go, typescript).")] string language,
      [Description("The class name to use for the core client class.")] string? className = null,
      [Description("The namespace to use for the core client class.")] string? namespaceName = null,
      [Description("Additional options for Kiota.")] string? additionalOptions = null)
  {
    return $"""
            You are an expert SDK generator using Microsoft Kiota.

            1. User Input Analysis
            - OpenAPI Source: "{openApiSource}"
            - Target Language: "{language}"
            - Configuration:
              - Class Name: {className ?? "Default (ApiClient)"}
              - Namespace: {namespaceName ?? "Default (ApiSdk)"}
              - Options: {additionalOptions ?? "None"}

            ---
            2. Execution Strategy (Follow Strictly)

            Step 1: Validate & Normalize Language
            Match the input to a valid Kiota identifier: [ CSharp, Go, Java, PHP, Python, Ruby, Shell, Swift, TypeScript ].
            - If a match or alias is found (e.g., "ts" -> "TypeScript", "golang" -> "Go"), use the valid identifier.
            - If NO match is found (e.g., "Rust", "C++", "asdf"), STOP immediately and ask the user to provide a supported language.

            Step 2: Resolve OpenAPI Source (CRITICAL)
            The 'generate_sdk' tool accepts either a URL or Raw Content, but NOT a file path.
            Analyze the [OpenAPI Source] provided above:
            
            - CASE A: It is a URL (starts with http/https)
              - Action: Pass the URL string directly to the `specSource` argument.
            
            - CASE B: It looks like a File Path (e.g., "C:\specs\api.json", "./swagger.yaml")
              - Action: You MUST first read the content of this file using your available tools (e.g., `filesystem` tool).
              - Then, pass the file content (JSON/YAML text) to the `specSource` argument.
              - If you cannot read the file, ask the user to paste the content directly.

            - CASE C: It is Raw JSON/YAML Content
              - Action: Pass the content string directly to the `specSource` argument.

            Step 3: Call Tool
            Call the `generate_sdk` tool with the prepared arguments:
            - `language`: (The normalized identifier from Step 1)
            - `specSource`: (The resolved URL or Content from Step 2)
            - `className`: (As provided)
            - `namespaceName`: (As provided)
            - `additionalOptions`: (As provided)

            Step 4: Report Results
            - If a download link is returned, display it clearly.
            - If a local path is returned, provide the path.
            """;
  }
}