using McpSamples.Shared.Configurations;

namespace McpSamples.OnedriveDownload.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for the onedrive-download app.
/// </summary>
public class OnedriveDownloadAppSettings : AppSettings
{
    /// <summary>
    /// Gets or sets the <see cref="EntraIdSettings"/> instance.
    /// </summary>
    public EntraIdSettings EntraId { get; set; } = new EntraIdSettings();
}

/// <summary>
/// This represents the Entra ID settings.
/// </summary>
public class EntraIdSettings
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
    /// Gets or sets the user-assigned client ID.
    /// </summary>
    public string? UserAssignedClientId { get; set; }

    /// <summary>
    /// Gets the value indicating whether to use the managed identity or not.
    /// </summary>
    public bool UseManagedIdentity => string.IsNullOrWhiteSpace(UserAssignedClientId) == false;
}