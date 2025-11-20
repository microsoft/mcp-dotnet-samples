using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;

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

            // Step 1: 기본 DefaultAzureCredential로 토큰 획득
            _logger.LogInformation("Step 1: Getting initial token from DefaultAzureCredential");
            var baseCredential = new DefaultAzureCredential();
            var baseToken = await baseCredential.GetTokenAsync(
                new TokenRequestContext(_scopes));

            // Step 2: JWT 토큰에서 tenantId 추출
            _logger.LogInformation("Step 2: Extracting tenantId from JWT token");
            string? homeTenantId = ExtractTenantIdFromJwt(baseToken.Token);

            if (string.IsNullOrEmpty(homeTenantId))
            {
                _logger.LogWarning("Could not extract tenant ID from token");
                return null;
            }

            _logger.LogInformation("✓ Extracted home tenant ID: {HomeTenantId}", homeTenantId);

            // Step 3: 추출한 tenantId로 Personal 365 토큰 획득
            _logger.LogInformation("Step 3: Acquiring Personal OneDrive token for tenant {HomeTenantId}", homeTenantId);

            var credentialOptions = new DefaultAzureCredentialOptions
            {
                TenantId = homeTenantId,
                VisualStudioTenantId = homeTenantId,
                SharedTokenCacheTenantId = homeTenantId
            };

            var m365Credential = new DefaultAzureCredential(credentialOptions);

            var m365Token = await m365Credential.GetTokenAsync(
                new TokenRequestContext(_oneDriveScopes));

            _logger.LogInformation("✓ Personal OneDrive token acquired successfully");
            return m365Token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Personal OneDrive access token: {ErrorMessage}", ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            return null;
        }
    }

    /// <summary>
    /// JWT 토큰에서 tenantId (tid) claim 추출
    /// </summary>
    private string? ExtractTenantIdFromJwt(string token)
    {
        try
        {
            _logger.LogInformation("Extracting tenantId from JWT token");
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
            {
                _logger.LogError("Token cannot be read as JWT");
                return null;
            }

            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken == null)
            {
                _logger.LogError("Failed to parse JWT token");
                return null;
            }

            var tidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid");
            if (tidClaim == null)
            {
                _logger.LogError("No 'tid' claim found in JWT token");
                _logger.LogInformation("Available claims: {Claims}",
                    string.Join(", ", jwtToken.Claims.Select(c => c.Type)));
                return null;
            }

            var tenantId = tidClaim.Value;
            _logger.LogInformation("✓ Successfully extracted tenantId: {TenantId}", tenantId);
            return tenantId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tenantId from JWT: {ErrorMessage}", ex.Message);
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
