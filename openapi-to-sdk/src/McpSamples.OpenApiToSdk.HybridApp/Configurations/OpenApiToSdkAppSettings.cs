using McpSamples.Shared.Configurations;
using Microsoft.OpenApi.Models;

namespace McpSamples.OpenApiToSdk.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for the OpenApiToSdk app.
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

    /// <summary>
    /// Gets or sets the <see cref="RuntimeSettings"/> instance.
    /// </summary>
    public RuntimeSettings Runtime { get; set; } = new RuntimeSettings();

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

    /// <inheritdoc />
    protected override T ParseMore<T>(IConfiguration config, string[] args)
    {
        var settings = base.ParseMore<T>(config, args);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--azure":
                case "-a":
                    (settings as OpenApiToSdkAppSettings)!.Runtime.Mode = "Azure";
                    break;

                case "--container":
                case "-c":
                    (settings as OpenApiToSdkAppSettings)!.Runtime.Mode = "Container";
                    break;

                default:
                    break;
            }
        }

        return settings;
    }
}

/// <summary>
/// This represents the runtime settings for the OpenApiToSdk app.
/// </summary>
public class RuntimeSettings
{
    /// <summary>
    /// Gets or sets the runtime mode (Local, Container, Azure).
    /// </summary>
    public string Mode { get; set; } = "Local";
}