using Microsoft.AspNetCore.Mvc;
using McpSamples.OnedriveDownload.HybridApp.Services;

namespace McpSamples.OnedriveDownload.HybridApp.Controllers;

/// <summary>
/// OAuth 2.0 인증 흐름을 처리하는 컨트롤러
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserAuthenticationService _userAuthenticationService;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserAuthenticationService userAuthenticationService,
        ITokenStorage tokenStorage,
        ILogger<AuthController> logger)
    {
        _userAuthenticationService = userAuthenticationService;
        _tokenStorage = tokenStorage;
        _logger = logger;
    }

    /// <summary>
    /// OAuth 로그인 시작
    /// GET /auth/login → Microsoft 로그인 페이지로 리다이렉트
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        _logger.LogInformation("=== Auth/Login called ===");

        try
        {
            var authUrl = _userAuthenticationService.GetAuthorizationUrl();
            _logger.LogInformation("Redirecting to authorization URL");
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authorization URL");
            return BadRequest(new { error = "Failed to initiate login", message = ex.Message });
        }
    }

    /// <summary>
    /// OAuth 콜백 처리
    /// GET /auth/callback?code=...&state=... → 토큰 교환 및 저장
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        _logger.LogInformation("=== Auth/Callback called ===");
        _logger.LogInformation("Code: {Code}, State: {State}", string.IsNullOrEmpty(code) ? "NULL" : "PRESENT", state);

        // 에러 확인
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Authorization error: {Error} - {Description}", error, error_description);
            return BadRequest(new
            {
                error = error,
                error_description = error_description
            });
        }

        // 인증 코드 확인
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("No authorization code received");
            return BadRequest(new { error = "No authorization code received" });
        }

        try
        {
            _logger.LogInformation("Exchanging authorization code for access token");

            // 인증 코드를 토큰으로 교환
            var userId = await _userAuthenticationService.GetAccessTokenFromCodeAsync(code);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Failed to get user ID from authentication");
                return BadRequest(new { error = "Failed to authenticate user" });
            }

            _logger.LogInformation("Authentication successful for user: {UserId}", userId);

            // 현재 사용자 설정
            await _tokenStorage.SetCurrentUserIdAsync(userId);

            // 성공 응답
            return Ok(new
            {
                message = "Authentication successful",
                userId = userId,
                redirectUrl = "/" // 클라이언트가 리다이렉트할 URL
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange authorization code: {ErrorMessage}", ex.Message);
            return StatusCode(500, new
            {
                error = "Authentication failed",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// 현재 인증 상태 확인
    /// GET /auth/status → 현재 사용자 정보
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        _logger.LogInformation("=== Auth/Status called ===");

        try
        {
            var userId = await _tokenStorage.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(userId))
            {
                return Ok(new
                {
                    authenticated = false,
                    message = "User not authenticated"
                });
            }

            var isExpired = await _tokenStorage.IsTokenExpiredAsync(userId);

            return Ok(new
            {
                authenticated = true,
                userId = userId,
                tokenExpired = isExpired,
                loginUrl = $"{Request.Scheme}://{Request.Host}/auth/login"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authentication status");
            return StatusCode(500, new { error = "Failed to get status" });
        }
    }

    /// <summary>
    /// 로그아웃
    /// POST /auth/logout → 토큰 삭제
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("=== Auth/Logout called ===");

        try
        {
            var userId = await _tokenStorage.GetCurrentUserIdAsync();

            if (!string.IsNullOrEmpty(userId))
            {
                await _tokenStorage.RemoveTokenAsync(userId);
                _logger.LogInformation("Logged out user: {UserId}", userId);
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout");
            return StatusCode(500, new { error = "Logout failed" });
        }
    }
}
