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
    public EntraIdSettings EntraId { get; set; } = new EntraIdSettings(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));
}

/// <summary>
/// This represents the Entra ID settings.
/// </summary>
/// <param name="userAssignedClientId">The user-assigned client ID from AZURE_CLIENT_ID environment variable.</param>
public class EntraIdSettings(string? userAssignedClientId = default)
{
    /// <summary>
    /// Gets or sets the tenant ID.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the client ID for Personal OneDrive OAuth.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets the user-assigned client ID from Azure Managed Identity.
    /// </summary>
    public string? UserAssignedClientId { get; } = userAssignedClientId;

    /// <summary>
    /// Gets the value indicating whether to use the managed identity or not.
    /// </summary>
    public bool UseManagedIdentity { get; } = string.IsNullOrWhiteSpace(userAssignedClientId) == false;

    /// <summary>
    /// Gets or sets the Personal 365 refresh token for OneDrive access.
    /// </summary>
    public string? Personal365RefreshToken { get; set; }
}