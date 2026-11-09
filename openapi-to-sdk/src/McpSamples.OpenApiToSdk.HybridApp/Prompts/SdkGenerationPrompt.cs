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
      [Description("The target programming language. Supported values: CSharp, Java, TypeScript, PHP, Python, Go, Ruby, Dart, HTTP.")]
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

        1. **Smart Language Normalization**:
           The `generate_sdk` tool ONLY accepts the following language identifiers:
           [ CSharp, Java, TypeScript, PHP, Python, Go, Ruby, Dart, HTTP ]

           You MUST intelligently map the user's input to one of these valid identifiers.
           
           - **Handle Aliases & Variations**:
             - "C#", "c#", ".NET", "dotnet", "chsarp" (typo) -> Use CSharp
             - "TS", "Ts", "ts", "node", "typoscript" (typo) -> Use TypeScript
             - "Golang", "Goo" (typo) -> Use Go
             - "py", "pyton" (typo), "python3" -> Use Python
             - "jav", "Jave" (typo) -> Use Java
           
           - **Auto-Correction**:
             - If the user makes a minor typo or uses a common abbreviation, automatically correct it to the nearest valid identifier from the list above.

           - **Validation**:
             - If the input refers to a completely unsupported language (e.g., "Rust", "C++", "Assembly"), STOP and politely inform the user that it is not currently supported by Kiota.

        2. **Handle Output Path**:
           - The `generate_sdk` tool manages the output path internally to create a ZIP file.
           - NEVER pass `-o` or `--output` in the `additionalOptions` argument, even if the user asks to save it to a specific location (e.g., "Generate to D:/Work").
           - Instead, follow this workflow:
             1. Call `generate_sdk` WITHOUT the output path option.
             2. Once the tool returns the ZIP file path (or download link), tell the user: "I have generated the SDK. Would you like me to move/extract it to [User's Requested Path]?"
             3. If the user agrees, use your filesystem tools to move the file.

        3. **Call the Tool**:
           Use the `generate_sdk` tool with the normalized language and filtered options (excluding -o).
           
        4. **Report**:
           Provide the download link or file path returned by the tool.
        """;
  }
}