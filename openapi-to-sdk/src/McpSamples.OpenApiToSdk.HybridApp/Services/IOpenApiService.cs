namespace McpSamples.OpenApiToSdk.HybridApp.Services;

public interface IOpenApiService
{
    /// <summary>
    /// Generates a client SDK from an OpenAPI specification.
    /// </summary>
    /// <param name="specSource">The URL or local file path of the OpenAPI spec.</param>
    /// <param name="language">The target programming language for the SDK.</param>
    /// <param name="clientClassName">The name of the generated client class (default: ApiClient).</param>
    /// <param name="namespaceName">The namespace for the generated code (default: ApiSdk).</param>
    /// <param name="additionalOptions">Additional Kiota command line options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A message indicating the result path or download URL.</returns>
    Task<string> GenerateSdkAsync(string specSource, string language, string? clientClassName, string? namespaceName, string? additionalOptions, CancellationToken cancellationToken = default);
}