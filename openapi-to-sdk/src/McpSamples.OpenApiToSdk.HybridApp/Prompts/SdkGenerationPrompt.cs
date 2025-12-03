using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Prompts;

/// <summary>
/// Defines the interface for SDK generation prompts.
/// </summary>
public interface ISdkGenerationPrompt
{
  /// <summary>
  /// Gets a prompt to guide the user in generating a client SDK.
  /// </summary>
  string GetSdkGenerationPrompt(
      string specSource,
      string language,
      string? clientClassName = "ApiClient",
      string? namespaceName = "ApiSdk",
      string? additionalOptions = "None");
}

/// <summary>
/// Represents the prompts for the OpenAPI to SDK generator.
/// </summary>
[McpServerPromptType]
public class SdkGenerationPrompt : ISdkGenerationPrompt
{
  /// <inheritdoc />
  [McpServerPrompt(Name = "generate_sdk_prompt", Title = "Prompt for generating client SDK")]
  [Description("A prompt to guide the user in generating a client SDK from an OpenAPI specification.")]
  public string GetSdkGenerationPrompt(
      [Description("The URL or local file path of the OpenAPI specification.")]
        string specSource,

      [Description("The target programming language. Supported values: csharp, go, java, php, python, ruby, shell, swift, typescript.")]
        string language,

      [Description("The name of the generated client class. Default: 'ApiClient'.")]
        string? clientClassName = "ApiClient",

      [Description("The namespace for the generated code. Default: 'ApiSdk'.")]
        string? namespaceName = "ApiSdk",

      [Description("Any additional options for Kiota generation (e.g., --version).")]
        string? additionalOptions = "None")
  {
    return $"""
        You are an expert SDK generator using Microsoft Kiota.

        Your task is to generate a client SDK based on the following inputs:
        - OpenAPI Source: `{specSource}`
        - Target Language: `{language}`
        - Configuration:
            - Class Name: {clientClassName}
            - Namespace: {namespaceName}
            - Additional Options: {additionalOptions}

        ---
        ### Execution Rules (Follow Strictly)

        1. **Validate & Normalize Language**:
           The `generate_sdk` tool ONLY accepts the following lowercase language identifiers:
           [ **csharp**, **go**, **java**, **php**, **python**, **ruby**, **shell**, **swift**, **typescript** ]

           - If the user input is "C#", ".NET", or "csharp", you MUST use **`csharp`**.
           - If the user input is "TypeScript", "ts", or "TS", you MUST use **`typescript`**.
           - If the input is not in the list (e.g., "Rust", "C++"), STOP and inform the user it is not supported.

        2. **Call the Tool**:
           Use the `generate_sdk` tool with the normalized language and provided parameters.
           
        3. **Report**:
           Provide the download link or file path returned by the tool.
        """;
  }
}