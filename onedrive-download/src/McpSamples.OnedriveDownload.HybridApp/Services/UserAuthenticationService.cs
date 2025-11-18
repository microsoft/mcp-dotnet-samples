using Microsoft.Graph;
using Microsoft.Identity.Client;
using Azure.Core;
using System.Net.Http.Headers;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Service for handling user OAuth authentication with Microsoft identity.
/// Uses Authorization Code Flow (OAuth 2.0) for web applications.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the authorization URL for user login.
    /// </summary>
    string GetAuthorizationUrl();

    /// <summary>
    /// Exchanges authorization code for access token.
    /// </summary>
    Task<string?> GetAccessTokenFromCodeAsync(string authorizationCode);

    /// <summary>
    /// Gets GraphServiceClient using user's access token.
    /// </summary>
    Task<GraphServiceClient> GetUserGraphClientAsync(string userId);

    /// <summary>
    /// Refreshes user's access token using refresh token.
    /// </summary>
    Task<bool> RefreshAccessTokenAsync(string userId);
}

/// <summary>
/// Implementation of user authentication service using MSAL (Microsoft Authentication Library).
/// Uses Authorization Code Flow for web applications (not Interactive Authentication).
/// </summary>
public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly IConfidentialClientApplication _confidentialClientApp;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly string _clientId;
    private readonly string _tenantId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

    public UserAuthenticationService(
        IConfiguration configuration,
        ITokenStorage tokenStorage,
        ILogger<UserAuthenticationService> logger)
    {
        _tokenStorage = tokenStorage;
        _logger = logger;

        // Get credentials from configuration
        _clientId = configuration["OnedriveDownload:EntraId:ClientId"]
            ?? Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__ClientId")
            ?? throw new InvalidOperationException("ClientId is not configured");

        _tenantId = configuration["OnedriveDownload:EntraId:TenantId"]
            ?? Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__TenantId")
            ?? throw new InvalidOperationException("TenantId is not configured");

        _clientSecret = configuration["OnedriveDownload:EntraId:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__ClientSecret")
            ?? string.Empty;

        _redirectUri = configuration["OnedriveDownload:EntraId:RedirectUri"]
            ?? Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__RedirectUri")
            ?? "https://func-onedrive-download-34ugypgdcsh76.azurewebsites.net/auth/callback";

        // Initialize MSAL Confidential Client Application
        // This is for Authorization Code Flow (server-to-server)
        var authority = $"https://login.microsoftonline.com/{_tenantId}";

        _confidentialClientApp = ConfidentialClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority(authority)
            .WithClientSecret(_clientSecret)
            .Build();

        _logger.LogInformation("UserAuthenticationService initialized. ClientId: {ClientId}, TenantId: {TenantId}", _clientId, _tenantId);
    }

    public string GetAuthorizationUrl()
    {
        // Build the authorization URL for user login
        var scopes = new[] { "https://graph.microsoft.com/.default", "offline_access" };
        var authUrl = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/authorize?" +
                      $"client_id={Uri.EscapeDataString(_clientId)}&" +
                      $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                      $"response_type=code&" +
                      $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}&" +
                      $"response_mode=query";

        _logger.LogInformation("Authorization URL generated");
        return authUrl;
    }

    public async Task<string?> GetAccessTokenFromCodeAsync(string authorizationCode)
    {
        try
        {
            _logger.LogInformation("Exchanging authorization code for access token");

            var result = await _confidentialClientApp
                .AcquireTokenByAuthorizationCode(
                    _scopes,
                    authorizationCode)
                .ExecuteAsync();

            var userId = result.Account?.HomeAccountId?.Identifier ?? result.Account?.Username ?? "unknown";

            // Save access token
            await _tokenStorage.SaveTokenAsync(userId, result.AccessToken, (int)result.ExpiresOn.Subtract(DateTime.UtcNow).TotalSeconds);

            // Note: Refresh token is managed internally by MSAL's token cache
            // MSAL automatically handles token refresh using the cache
            _logger.LogInformation("Access token saved for user: {UserId}", userId);

            _logger.LogInformation("Access token obtained for user: {UserId}", userId);
            return userId;
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "Failed to exchange authorization code: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    public async Task<GraphServiceClient> GetUserGraphClientAsync(string userId)
    {
        try
        {
            _logger.LogInformation("=== GetUserGraphClientAsync called ===");
            _logger.LogInformation("Attempting to get access token for userId: {UserId}", userId);

            var accessToken = await _tokenStorage.GetAccessTokenAsync(userId);
            _logger.LogInformation("Token Storage returned: {TokenStatus}", string.IsNullOrEmpty(accessToken) ? "NULL/EMPTY" : "TOKEN_FOUND");

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Access token is empty for userId: {UserId}", userId);
                // Try to refresh the token
                var refreshToken = await _tokenStorage.GetRefreshTokenAsync(userId);
                _logger.LogInformation("Refresh token status: {RefreshTokenStatus}", string.IsNullOrEmpty(refreshToken) ? "NULL/EMPTY" : "FOUND");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshed = await RefreshAccessTokenAsync(userId);
                    if (!refreshed)
                    {
                        _logger.LogError("Token refresh failed for userId: {UserId}", userId);
                        throw new InvalidOperationException($"No valid access token for user: {userId}");
                    }
                    accessToken = await _tokenStorage.GetAccessTokenAsync(userId);
                }
                else
                {
                    _logger.LogError("No refresh token found for userId: {UserId}", userId);
                    throw new InvalidOperationException($"No valid token for user: {userId}");
                }
            }

            _logger.LogInformation("Access token acquired successfully for userId: {UserId}", userId);

            // Create GraphServiceClient with user's token
            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            _logger.LogInformation("GraphServiceClient created for user: {UserId}", userId);
            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GraphServiceClient for user {UserId}: {ErrorMessage}", userId, ex.Message);
            throw;
        }
    }

    public async Task<bool> RefreshAccessTokenAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Attempting to refresh access token for user: {UserId}", userId);

            var token = await _tokenStorage.GetAccessTokenAsync(userId);

            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Token still valid for user: {UserId}", userId);
                return true;
            }

            _logger.LogWarning("Token expired for user: {UserId}. User needs to re-authenticate.", userId);
            await _tokenStorage.RemoveTokenAsync(userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token for user {UserId}: {ErrorMessage}", userId, ex.Message);
            return false;
        }
    }
}
