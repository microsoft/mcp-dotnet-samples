using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

    public UserAuthenticationService(ILogger<UserAuthenticationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
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

    // ★★★ 여기가 핵심 수정됨: 실패 이유를 낱낱이 밝히는 로직 ★★★
    public async Task<string?> GetPersonalOneDriveAccessTokenAsync()
    {
        // 1. 환경변수 뒤지기 (시스템 변수 우선)
        var refreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // 백업: App Settings 확인
            refreshToken = _configuration["PERSONAL_365_REFRESH_TOKEN"]
                           ?? _configuration["OnedriveDownload:EntraId:Personal365RefreshToken"];
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new Exception("CRITICAL: 환경변수 'PERSONAL_365_REFRESH_TOKEN'이 비어있습니다. Azure Portal 설정을 확인하세요.");
        }

        // 공백/따옴표 제거
        refreshToken = refreshToken.Trim().Trim('"').Trim('\'');

        var clientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e"; // 개인용 공용 Client ID

        using var httpClient = new HttpClient();
        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            // ★ 스코프: Files.Read.All 포함 확인
            new KeyValuePair<string, string>("scope", "Files.Read.All User.Read offline_access")
        });

        // 2. Microsoft 서버로 요청 (consumers 엔드포인트)
        var response = await httpClient.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", body);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // ★ 에러를 삼키지 않고 그대로 던짐
            throw new Exception($"[MS Login Failed] Status: {response.StatusCode} | Body: {responseContent}");
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            return tokenElement.GetString();
        }

        throw new Exception($"[Token Parse Error] 응답에 access_token 없음. Body: {responseContent}");
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
