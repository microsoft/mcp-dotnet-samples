using Azure.Core;

namespace McpSamples.OnedriveDownload.HybridApp.Authentication;

/// <summary>
/// Token Passthrough: HTTP 요청 헤더에서 토큰을 꺼내 사용하는 Credential
/// VSCode에서 인증 후 보낸 Bearer 토큰을 그대로 사용
/// </summary>
public class BearerTokenCredential : TokenCredential
{
    private readonly string _token;

    public BearerTokenCredential(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Token cannot be null or empty", nameof(token));

        _token = token;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Token은 이미 유효하다고 가정 (클라이언트가 보낸 것이므로)
        // 실제로는 클라이언트가 토큰을 유효한 상태로 유지해야 함
        return new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
    }
}

/// <summary>
/// Anonymous 인증: 토큰이 없을 때 사용
/// VSCode가 "이 서버는 토큰이 필요하다"고 감지하게 하는 신호 역할
/// </summary>
public class AnonymousTokenCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // 빈 토큰 반환 - 이것으로 VSCode가 인증이 필요함을 알게 됨
        return new AccessToken(string.Empty, DateTimeOffset.UtcNow);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
    }
}
