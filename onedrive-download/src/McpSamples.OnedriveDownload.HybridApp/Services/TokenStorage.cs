using System.Collections.Concurrent;
using Microsoft.Identity.Client;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// In-memory token cache for storing user OAuth tokens.
/// Note: For production, use distributed cache (Redis) or persistent storage.
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// Gets the access token for a user.
    /// </summary>
    Task<string?> GetAccessTokenAsync(string userId);

    /// <summary>
    /// Saves the access token for a user.
    /// </summary>
    Task SaveTokenAsync(string userId, string accessToken, int expiresInSeconds);

    /// <summary>
    /// Gets the refresh token for a user.
    /// </summary>
    Task<string?> GetRefreshTokenAsync(string userId);

    /// <summary>
    /// Saves the refresh token for a user.
    /// </summary>
    Task SaveRefreshTokenAsync(string userId, string refreshToken);

    /// <summary>
    /// Checks if the token is expired.
    /// </summary>
    Task<bool> IsTokenExpiredAsync(string userId);

    /// <summary>
    /// Removes the token for a user (logout).
    /// </summary>
    Task RemoveTokenAsync(string userId);
}

/// <summary>
/// In-memory implementation of token storage.
/// </summary>
public class InMemoryTokenStorage : ITokenStorage
{
    private class TokenInfo
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, TokenInfo> _tokens = new();

    public Task<string?> GetAccessTokenAsync(string userId)
    {
        if (_tokens.TryGetValue(userId, out var token))
        {
            if (DateTime.UtcNow < token.ExpiresAt)
            {
                return Task.FromResult(token.AccessToken);
            }
            else
            {
                // Token expired, remove it
                _tokens.TryRemove(userId, out _);
            }
        }

        return Task.FromResult<string?>(null);
    }

    public Task SaveTokenAsync(string userId, string accessToken, int expiresInSeconds)
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        };

        _tokens.AddOrUpdate(userId, tokenInfo, (key, old) =>
        {
            tokenInfo.RefreshToken = old.RefreshToken;
            return tokenInfo;
        });

        return Task.CompletedTask;
    }

    public Task<string?> GetRefreshTokenAsync(string userId)
    {
        if (_tokens.TryGetValue(userId, out var token))
        {
            return Task.FromResult(token.RefreshToken);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SaveRefreshTokenAsync(string userId, string refreshToken)
    {
        var tokenInfo = new TokenInfo
        {
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.MaxValue
        };

        _tokens.AddOrUpdate(userId, tokenInfo, (key, old) =>
        {
            tokenInfo.AccessToken = old.AccessToken;
            tokenInfo.ExpiresAt = old.ExpiresAt;
            return tokenInfo;
        });

        return Task.CompletedTask;
    }

    public Task<bool> IsTokenExpiredAsync(string userId)
    {
        if (_tokens.TryGetValue(userId, out var token))
        {
            return Task.FromResult(DateTime.UtcNow >= token.ExpiresAt);
        }

        return Task.FromResult(true);
    }

    public Task RemoveTokenAsync(string userId)
    {
        _tokens.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
