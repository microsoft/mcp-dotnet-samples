using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

public interface IUserAuthenticationService
{
    Task<string?> GetCurrentUserAccessTokenAsync();
    Task<GraphServiceClient> GetUserGraphClientAsync();
    Task<(GraphServiceClient? client, string? errorMessage)> GetPersonalOneDriveGraphClientAsync();
}

public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly GraphServiceClient _graphServiceClient;
    private readonly string[] _scopes = new[] { "https://graph.microsoft.com/.default" };

    public UserAuthenticationService(
        ILogger<UserAuthenticationService> logger,
        GraphServiceClient graphServiceClient)
    {
        _logger = logger;
        _graphServiceClient = graphServiceClient;
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

    // ★ GraphServiceClient은 Program.cs에서 InteractiveBrowserCredential로 초기화됨
    // GraphServiceClient이 이미 대화형 인증을 처리하므로, 이 메서드는 단순히 클라이언트를 반환
    public Task<(GraphServiceClient? client, string? errorMessage)> GetPersonalOneDriveGraphClientAsync()
    {
        try
        {
            return Task.FromResult<(GraphServiceClient?, string?)>((_graphServiceClient, null));
        }
        catch (Exception ex)
        {
            var fullError = $"[Auth Error] {ex.Message}";
            _logger.LogError(ex, fullError);
            return Task.FromResult<(GraphServiceClient?, string?)>((null, fullError));
        }
    }
}
