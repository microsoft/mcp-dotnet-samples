using System.IdentityModel.Tokens.Jwt;
using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.OnedriveDownload.HybridApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;

namespace McpSamples.OnedriveDownload.HybridApp.Controllers;

/// <summary>
/// OAuth authentication controller for handling Microsoft Entra login.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly OnedriveDownloadAppSettings _settings;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        OnedriveDownloadAppSettings settings,
        ITokenStorage tokenStorage,
        ILogger<AuthController> logger)
    {
        _settings = settings;
        _tokenStorage = tokenStorage;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the OAuth login flow by redirecting to Microsoft Entra login.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? redirectUrl)
    {
        var tenantId = _settings.EntraId.TenantId;
        var clientId = _settings.EntraId.ClientId;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
        {
            _logger.LogError("TenantId or ClientId is not configured");
            return BadRequest("OAuth is not properly configured");
        }

        var callbackUrl = Url.Action("Callback", "Auth", null, Request.Scheme);
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            redirectUrl ?? Url.Content("~/") ?? ""));

        // Build the authorization URL
        var authorizationUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize?" +
            $"client_id={Uri.EscapeDataString(clientId)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString("https://graph.microsoft.com/.default offline_access")}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl!)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&response_mode=query";

        _logger.LogInformation("Redirecting to OAuth login URL");
        return Redirect(authorizationUrl);
    }

    /// <summary>
    /// OAuth callback endpoint that receives the authorization code.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("OAuth error: {Error}", error);
            return BadRequest($"OAuth error: {error}");
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("Authorization code not provided");
            return BadRequest("Authorization code not provided");
        }

        try
        {
            var tenantId = _settings.EntraId.TenantId;
            var clientId = _settings.EntraId.ClientId;
            var clientSecret = _settings.EntraId.ClientSecret;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("OAuth configuration is missing");
                return BadRequest("OAuth is not properly configured");
            }

            var callbackUrl = Url.Action("Callback", "Auth", null, Request.Scheme);

            // Exchange authorization code for tokens
            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}/v2.0")
                .WithRedirectUri(callbackUrl!)
                .Build();

            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var result = await app.AcquireTokenByAuthorizationCode(scopes, code).ExecuteAsync();

            // Extract user ID from token
            var jwtHandler = new JwtSecurityTokenHandler();
            var token = jwtHandler.ReadToken(result.IdToken) as JwtSecurityToken;
            var userId = token?.Claims.FirstOrDefault(c => c.Type == "oid")?.Value ??
                        token?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ??
                        "unknown";

            // Save tokens
            await _tokenStorage.SaveTokenAsync(userId, result.AccessToken, (int)(result.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds);

            // Store userId in session for MCP context
            HttpContext.Session.SetString("UserId", userId);

            _logger.LogInformation("User {UserId} successfully authenticated", userId);

            // Redirect to the original URL or home
            var redirectUrl = state != null ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state)) : null;
            return Redirect(redirectUrl ?? "/");
        }
        catch (MsalException msalEx)
        {
            _logger.LogError(msalEx, "MSAL error during token acquisition");
            return BadRequest($"Authentication failed: {msalEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth callback");
            return BadRequest($"Authentication failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current user's OAuth status.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(new { authenticated = false });
        }

        var hasToken = await _tokenStorage.GetAccessTokenAsync(userId) != null;
        var isExpired = await _tokenStorage.IsTokenExpiredAsync(userId);

        return Ok(new
        {
            authenticated = true,
            userId = userId,
            tokenExpired = isExpired
        });
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (!string.IsNullOrEmpty(userId))
        {
            await _tokenStorage.RemoveTokenAsync(userId);
            HttpContext.Session.Remove("UserId");
            _logger.LogInformation("User {UserId} logged out", userId);
        }

        return Ok(new { message = "Logged out successfully" });
    }
}
