using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using McpSamples.OnedriveDownload.HybridApp.Configurations;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

public interface IUserAuthenticationService
{
    Task<string?> GetCurrentUserAccessTokenAsync();
    Task<string?> GetPersonalOneDriveAccessTokenAsync();
    Task<GraphServiceClient> GetUserGraphClientAsync();
    // 튜플 반환 (Client, ErrorMessage)
    Task<(GraphServiceClient? client, string? errorMessage)> GetPersonalOneDriveGraphClientAsync();
}

public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string[] _scopes = new[] { "https://graph.microsoft.com/.default" };

    // ★ OAuth2 토큰 저장소 (Program.cs에서 등록된 싱글톤)
    private OAuthTokenStore? _tokenStore;

    public UserAuthenticationService(
        ILogger<UserAuthenticationService> logger,
        IConfiguration configuration,
        OAuthTokenStore? tokenStore = null)
    {
        _logger = logger;
        _configuration = configuration;
        _tokenStore = tokenStore;
    }

    public async Task<string?> GetCurrentUserAccessTokenAsync()
    {
        try
        {
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(_scopes);
            var token = await credential.GetTokenAsync(tokenRequestContext);
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Azure credential token");
            return null;
        }
    }

    public Task<GraphServiceClient> GetUserGraphClientAsync()
    {
        var credential = new DefaultAzureCredential();
        return Task.FromResult(new GraphServiceClient(credential, _scopes));
    }

    // ★★★ OAuth2 Authorization Code Flow 토큰 획득 ★★★
    public async Task<string?> GetPersonalOneDriveAccessTokenAsync()
    {
        try
        {
            // 저장된 토큰이 없으면 null 반환 (사용자가 /login으로 가야 함)
            if (_tokenStore == null)
            {
                _logger.LogWarning("⚠️ OAuthTokenStore가 주입되지 않았습니다. /login으로 이동하세요.");
                return null;
            }

            // 저장된 액세스 토큰이 있고 만료되지 않았으면 사용
            if (!string.IsNullOrEmpty(_tokenStore.AccessToken) && DateTime.UtcNow < _tokenStore.ExpiresAt)
            {
                _logger.LogInformation("✓ 저장된 액세스 토큰 사용");
                return _tokenStore.AccessToken;
            }

            // 리프레시 토큰이 있으면 새 액세스 토큰 획득
            if (!string.IsNullOrEmpty(_tokenStore.RefreshToken))
            {
                _logger.LogInformation("🔄 리프레시 토큰으로 새 액세스 토큰 획득 중...");
                var newAccessToken = await RefreshAccessTokenAsync(_tokenStore.RefreshToken);
                if (!string.IsNullOrEmpty(newAccessToken))
                {
                    _logger.LogInformation("✓ 새 액세스 토큰 획득 성공");
                    return newAccessToken;
                }
            }

            // 토큰이 없으면 사용자에게 로그인 유도
            _logger.LogError("❌ 저장된 토큰이 없습니다. /login으로 이동하여 인증하세요.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 개인 OneDrive 액세스 토큰 획득 실패");
            return null;
        }
    }

    // ★★★ 리프레시 토큰으로 새 액세스 토큰 획득 ★★★
    private async Task<string?> RefreshAccessTokenAsync(string refreshToken)
    {
        try
        {
            var settings = _configuration.Get<OnedriveDownloadAppSettings>();
            if (settings?.EntraId == null)
            {
                _logger.LogError("❌ EntraId 설정을 찾을 수 없습니다.");
                return null;
            }

            var tenantId = settings.EntraId?.TenantId ?? string.Empty;
            var clientId = settings.EntraId?.ClientId ?? string.Empty;
            var clientSecret = settings.EntraId?.ClientSecret ?? string.Empty;

            using var httpClient = new HttpClient();
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default")
            });

            var response = await httpClient.PostAsync(
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                tokenRequest);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ 토큰 리프레시 실패: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            // 새로운 리프레시 토큰이 있으면 갱신 (없으면 기존 것 유지)
            var newRefreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
                ? refreshTokenElement.GetString()
                : refreshToken;

            // 토큰 저장소 갱신
            if (_tokenStore != null)
            {
                _tokenStore.AccessToken = accessToken;
                _tokenStore.RefreshToken = newRefreshToken;
                _tokenStore.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            }

            _logger.LogInformation("✓ 토큰 리프레시 성공");
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 토큰 리프레시 중 오류 발생");
            return null;
        }
    }



    // ★★★ 여기가 문제였음: 예외를 잡아서 'errorMessage'에 넣도록 수정 ★★★
    public async Task<(GraphServiceClient? client, string? errorMessage)> GetPersonalOneDriveGraphClientAsync()
    {
        try
        {
            // 위에서 에러가 나면 throw되므로 catch로 넘어감
            var accessToken = await GetPersonalOneDriveAccessTokenAsync();

            // 혹시라도 null이면 (로직상 그럴 일 없지만)
            if (string.IsNullOrEmpty(accessToken))
            {
                return (null, "Unknown Error: Access Token is null but no exception was thrown.");
            }

            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            return (graphClient, null); // 성공 시 에러 메시지 null
        }
        catch (Exception ex)
        {
            // ★ 예외 내용을 그대로 리턴해서 사용자가 보게 함
            var fullError = $"[Auth Error] {ex.Message}";
            _logger.LogError(ex, fullError);
            return (null, fullError);
        }
    }
}
