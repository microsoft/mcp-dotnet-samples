using System.ComponentModel;
using McpSamples.OpenApiToSdk.HybridApp.Services;
using ModelContextProtocol.Server;

namespace McpSamples.OpenApiToSdk.HybridApp.Tools;

/// <summary>
/// Defines the interface for the OpenAPI to SDK tool.
/// </summary>
public interface IOpenApiToSdkTool
{
  /// <summary>
  /// Generates a client SDK from an OpenAPI specification.
  /// </summary>
  Task<string> GenerateSdkAsync(
      string specSource,
      string language,
      string? clientClassName = null,
      string? namespaceName = null,
      string? additionalOptions = null);
}

/// <summary>
/// Represents the tool for generating client SDKs from OpenAPI specifications.
/// </summary>
/// <param name="service"><see cref="IOpenApiService"/> instance.</param>
[McpServerToolType]
public class OpenApiToSdkTool(IOpenApiService service) : IOpenApiToSdkTool
{
  /// <inheritdoc />
  [McpServerTool(Name = "generate_sdk", Title = "Generates a client SDK")]
  [Description("Generates a client SDK from an OpenAPI specification URL or local file path.")]
  public async Task<string> GenerateSdkAsync(
      [Description("The URL or local file path of the OpenAPI specification.")]
        string specSource,

      [Description("The target programming language (e.g., CSharp, Python, Java, TypeScript).")]
        string language,

      [Description("The name of the generated client class. Default is 'ApiClient'.")]
        string? clientClassName = null,

      [Description("The namespace for the generated code. Default is 'ApiSdk'.")]
        string? namespaceName = null,

      [Description("Additional Kiota command line options (e.g., --version).")]
        string? additionalOptions = null)
  {
    // Service 호출 (복잡한 파라미터 파싱 로직이 사라지고 바로 호출 가능)
    var resultMessage = await service.GenerateSdkAsync(
        specSource,
        language,
        clientClassName,
        namespaceName,
        additionalOptions);

    return resultMessage;
  }
}