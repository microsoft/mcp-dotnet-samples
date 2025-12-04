using McpSamples.Shared.Configurations;
using Microsoft.OpenApi.Models;

namespace McpSamples.OpenApiToSdk.HybridApp.Configurations;

/// <summary>
/// Represents the application settings for the OpenApiToSdk app.
/// Inherits from Shared AppSettings to maintain consistency.
/// </summary>
public class OpenApiToSdkAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP OpenAPI to SDK",
        Version = "1.0.0",
        Description = "An MCP server that generates client SDKs from OpenAPI specifications using Kiota."
    };

    // --------------------------------------------------------
    // Runtime Configurations (Values assigned after calculation in Program.cs)
    // --------------------------------------------------------

    /// <summary>
    /// The root path for the workspace (shared volume or local folder).
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// The path where generated SDKs (zip files) will be stored.
    /// </summary>
    public string GeneratedPath { get; set; } = string.Empty;

    /// <summary>
    /// The path where spec files are stored (or mounted).
    /// </summary>
    public string SpecsPath { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the app is running in HTTP mode (vs Stdio).
    /// </summary>
    public bool IsHttpMode { get; set; }

    /// <summary>
    /// Indicates if the app is running inside a Docker container.
    /// </summary>
    public bool IsContainer { get; set; }

    /// <summary>
    /// Indicates if the app is running in Azure Container Apps.
    /// </summary>
    public bool IsAzure { get; set; }
}