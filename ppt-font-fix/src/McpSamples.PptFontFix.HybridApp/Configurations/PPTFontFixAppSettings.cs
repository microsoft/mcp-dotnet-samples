using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.PptFontFix.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for ppt-font-fix app.
/// </summary>
public class PptFontFixAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP PPT Font Fix",
        Version = "1.0.0",
        Description = "A simple MCP server for managing PPT font fixing."
    };

    /// <summary>
    /// The root path for the workspace.
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// The path for generated files.
    /// </summary>
    public string GeneratedPath { get; set; } = string.Empty;
    
    /// <summary>
    /// The path for input files.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the application is running in HTTP mode.
    /// </summary>
    public bool IsHttpMode { get; set; }
    /// <summary>
    /// Indicates whether the application is running in a containerized environment.
    /// </summary>
    public bool IsContainer { get; set; }
    /// <summary>
    /// Indicates whether the application is running in an Azure environment.
    /// </summary>
    public bool IsAzure { get; set; }

    /// <inheritdoc />
    protected override T ParseMore<T>(IConfiguration config, string[] args)
    {
        var settings = base.ParseMore<T>(config, args);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (args.Contains("-c") || args.Contains("--container"))
            {
                (settings as PptFontFixAppSettings)!.IsContainer = true;
            }
        }

        return (settings as T)!;
    }
}