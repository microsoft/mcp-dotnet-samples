using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Service for handling user authentication using Azure DefaultCredential and OAuth.
/// Supports both Azure credentials (azd auth login) and Personal Microsoft account authentication.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the current user's access token from Azure credentials.
    /// </summary>
    Task<string?> GetCurrentUserAccessTokenAsync();

    /// <summary>
    /// Gets the Personal OneDrive access token using OAuth (MSAL).
    /// </summary>
    Task<string?> GetPersonalOneDriveAccessTokenAsync();

    /// <summary>
    /// Gets GraphServiceClient using Azure credentials.
    /// </summary>
    Task<GraphServiceClient> GetUserGraphClientAsync();

    /// <summary>
    /// Gets GraphServiceClient using Personal OneDrive token.
    /// </summary>
    Task<GraphServiceClient> GetPersonalOneDriveGraphClientAsync();
}

/// <summary>
/// Implementation of user authentication service using Azure DefaultCredential and MSAL.
/// Automatically retrieves tokens from azd auth login or other Azure credential sources.
/// Also supports Personal OneDrive access via OAuth.
/// </summary>
public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };
    private readonly string[] _oneDriveScopes = { "https://graph.microsoft.com/Files.ReadWrite.All" };

    public UserAuthenticationService(
        ILogger<UserAuthenticationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string?> GetCurrentUserAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("=== GetCurrentUserAccessTokenAsync called ===");
            _logger.LogInformation("Attempting to get token from Azure DefaultCredential");

            // Use DefaultAzureCredential which includes:
            // - azd auth login token
            // - Environment variables
            // - Visual Studio credentials
            // - Azure PowerShell credentials
            // - Azure CLI credentials
            var credential = new DefaultAzureCredential();

            var token = await credential.GetTokenAsync(
                new TokenRequestContext(_scopes));

            _logger.LogInformation("Access token acquired from Azure DefaultCredential");
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token from Azure DefaultCredential: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    public async Task<string?> GetPersonalOneDriveAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("=== GetPersonalOneDriveAccessTokenAsync called ===");

            // Step 1: Refresh token 확인
            _logger.LogInformation("Step 1: Checking for Personal 365 refresh token in configuration");
            var refreshToken = _configuration?["EntraId:Personal365RefreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogError("Step 1 failed: No Personal 365 refresh token found in configuration");
                _logger.LogError("Please run 'azd provision' to acquire a refresh token");
                return null;
            }

            _logger.LogInformation("✓ Step 1 completed: Refresh token found");

            // Step 2: Refresh token으로 새 access token 획득
            _logger.LogInformation("Step 2: Acquiring access token using refresh token");
            try
            {
                var accessToken = await GetAccessTokenFromRefreshTokenAsync(refreshToken);

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Step 2 failed: Could not get access token from refresh token");
                    return null;
                }

                _logger.LogInformation("✓ Step 2 completed: Access token acquired successfully");
                return accessToken;
            }
            catch (Exception stepEx)
            {
                _logger.LogError(stepEx, "Step 2 failed: Exception while acquiring access token. Exception: {ExceptionType} - {Message}",
                    stepEx.GetType().Name, stepEx.Message);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPersonalOneDriveAccessTokenAsync failed: {ErrorMessage}", ex.Message);
            _logger.LogError("Exception type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return null;
        }
    }

    /// <summary>
    /// Refresh token으로 새 access token 획득 (consumers 테넌트)
    /// </summary>
    private async Task<string?> GetAccessTokenFromRefreshTokenAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("GetAccessTokenFromRefreshTokenAsync: Requesting new access token");

            var clientId = _configuration?["EntraId:ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("GetAccessTokenFromRefreshTokenAsync: ClientId not found in configuration");
                return null;
            }

            using var httpClient = new System.Net.Http.HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" },
                { "scope", "Files.Read User.Read offline_access" }
            };

            var content = new System.Net.Http.FormUrlEncodedContent(requestBody);

            var response = await httpClient.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("GetAccessTokenFromRefreshTokenAsync: Token endpoint returned error. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var accessTokenElement))
            {
                var accessToken = accessTokenElement.GetString();
                _logger.LogInformation("✓ GetAccessTokenFromRefreshTokenAsync: New access token acquired");
                return accessToken;
            }

            _logger.LogError("GetAccessTokenFromRefreshTokenAsync: No access_token in response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAccessTokenFromRefreshTokenAsync: Exception: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            return null;
        }
    }

    public async Task<GraphServiceClient> GetUserGraphClientAsync()
    {
        try
        {
            _logger.LogInformation("=== GetUserGraphClientAsync called ===");

            var accessToken = await GetCurrentUserAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token available from Azure credentials");
                throw new InvalidOperationException("No access token found. Please ensure you are authenticated via 'azd auth login'.");
            }

            _logger.LogInformation("Access token acquired successfully");

            // Create GraphServiceClient with user's token
            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            _logger.LogInformation("GraphServiceClient created for authenticated user");
            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GraphServiceClient: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    public async Task<GraphServiceClient> GetPersonalOneDriveGraphClientAsync()
    {
        try
        {
            _logger.LogInformation("=== GetPersonalOneDriveGraphClientAsync called ===");

            var accessToken = await GetPersonalOneDriveAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token available for Personal OneDrive");
                throw new InvalidOperationException("Failed to acquire Personal OneDrive access token");
            }

            _logger.LogInformation("Personal OneDrive token acquired successfully");

            // Create GraphServiceClient with Personal OneDrive token
            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            _logger.LogInformation("GraphServiceClient created for Personal OneDrive");
            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Personal OneDrive GraphServiceClient: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
