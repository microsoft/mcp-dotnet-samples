using Microsoft.Graph;
using System.Net.Http.Headers;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Service for handling user authentication using tokens from HTTP context.
/// Supports user-delegated authentication where the client automatically provides user tokens.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the current user's access token from HTTP context.
    /// </summary>
    Task<string?> GetCurrentUserAccessTokenAsync();

    /// <summary>
    /// Gets GraphServiceClient using the current user's access token.
    /// </summary>
    Task<GraphServiceClient> GetUserGraphClientAsync();

    /// <summary>
    /// Extracts user principal name from the token claims.
    /// </summary>
    Task<string?> GetCurrentUserPrincipalNameAsync();
}

/// <summary>
/// Implementation of user authentication service using tokens from HTTP context.
/// The client automatically provides user tokens (e.g., from azd auth login).
/// </summary>
public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserAuthenticationService> _logger;

    public UserAuthenticationService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserAuthenticationService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Task<string?> GetCurrentUserAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("=== GetCurrentUserAccessTokenAsync called ===");

            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogWarning("HttpContext is null");
                return Task.FromResult<string?>(null);
            }

            // Extract token from Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                _logger.LogWarning("Authorization header not found");
                return Task.FromResult<string?>(null);
            }

            // Expected format: "Bearer {token}"
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Authorization header does not start with 'Bearer'");
                return Task.FromResult<string?>(null);
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            _logger.LogInformation("Access token extracted from Authorization header");
            return Task.FromResult<string?>(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token from context: {ErrorMessage}", ex.Message);
            return Task.FromResult<string?>(null);
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
                _logger.LogError("No access token available in HTTP context");
                throw new InvalidOperationException("No access token found. User authentication is required.");
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

    public Task<string?> GetCurrentUserPrincipalNameAsync()
    {
        try
        {
            // Try to extract from X-MS-CLIENT-PRINCIPAL-NAME header (Azure App Service)
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return Task.FromResult<string?>(null);
            }

            var principalName = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
            if (!string.IsNullOrEmpty(principalName))
            {
                _logger.LogInformation("Principal name extracted: {PrincipalName}", principalName);
                return Task.FromResult<string?>(principalName);
            }

            // Try to extract from X-FORWARDED-USER header (Common proxy header)
            var forwardedUser = context.Request.Headers["X-FORWARDED-USER"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedUser))
            {
                _logger.LogInformation("Principal name extracted from X-FORWARDED-USER: {PrincipalName}", forwardedUser);
                return Task.FromResult<string?>(forwardedUser);
            }

            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get principal name: {ErrorMessage}", ex.Message);
            return Task.FromResult<string?>(null);
        }
    }
}
