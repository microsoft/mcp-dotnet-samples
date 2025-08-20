using McpSamples.Shared.Configurations;

using Microsoft.OpenApi.Models;

namespace McpSamples.OutlookEmail.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for outlook-email app.
/// </summary>
public class OutlookEmailAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = "MCP Outlook Email",
        Version = "1.0.0",
        Description = "A simple MCP server for sending emails through Outlook."
    };

    /// <summary>
    /// Gets or sets the <see cref="EntraIdSettings"/> instance.
    /// </summary>
    public EntraIdSettings EntraId { get; set; } = new EntraIdSettings(Environment.GetEnvironmentVariable(Constants.AzureClientIdEnvironmentKey));

    /// <inheritdoc />
    protected override T ParseMore<T>(IConfiguration config, string[] args)
    {
        var settings = base.ParseMore<T>(config, args);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--tenant-id":
                case "-t":
                    (settings as OutlookEmailAppSettings)!.EntraId.TenantId = args[++i];
                    break;

                case "--client-id":
                case "-c":
                    (settings as OutlookEmailAppSettings)!.EntraId.ClientId = args[++i];
                    break;

                case "--client-secret":
                case "-s":
                    (settings as OutlookEmailAppSettings)!.EntraId.ClientSecret = args[++i];
                    break;

                default:
                    settings.Help = true;
                    break;
            }
        }

        return settings;
    }
}

/// <summary>
/// This represents the Entra ID settings.
/// </summary>
/// <param name="userAssignedClientId">The user-assigned client ID.</param>
public class EntraIdSettings(string? userAssignedClientId = default)
{
    /// <summary>
    /// Gets or sets the tenant ID.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets the value indicating whether to use the managed identity or not.
    /// </summary>
    public bool UseManagedIdentity { get; } = string.IsNullOrWhiteSpace(userAssignedClientId) == false;

    /// <summary>
    /// Gets the user-assigned client ID.
    /// </summary>
    public string? UserAssignedClientId { get; } = userAssignedClientId;
}