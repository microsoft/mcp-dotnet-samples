using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Service for handling user authentication using Azure DefaultCredential.
/// Automatically gets tokens from azd auth login or other Azure credentials.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the current user's access token from Azure credentials.
    /// </summary>
    Task<string?> GetCurrentUserAccessTokenAsync();

    /// <summary>
    /// Gets GraphServiceClient using Azure credentials.
    /// </summary>
    Task<GraphServiceClient> GetUserGraphClientAsync();
}

/// <summary>
/// Implementation of user authentication service using Azure DefaultCredential.
/// Automatically retrieves tokens from azd auth login or other Azure credential sources.
/// </summary>
public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

    public UserAuthenticationService(
        ILogger<UserAuthenticationService> logger)
    {
        _logger = logger;
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
}
