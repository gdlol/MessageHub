using System.Collections.Concurrent;
using System.Web;

namespace MessageHub.HomeServer.Dummy;

public class DummyAuthenticator : IAuthenticator
{
    private readonly string loginToken;
    private readonly string userId;
    private readonly string accessTokenPrefix;

    public DummyAuthenticator(IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);

        loginToken = peerIdentity.Id;
        accessTokenPrefix = peerIdentity.Id;
        userId = UserIdentifier.FromId(peerIdentity.Id).ToString();
    }

    private readonly ConcurrentDictionary<string, object?> accessTokens = new();

    public string GetSsoRedirectUrl(string redirectUrl)
    {
        ArgumentNullException.ThrowIfNull(redirectUrl);

        var uriBuilder = new UriBuilder(redirectUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query[nameof(loginToken)] = loginToken;
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    public Task<string?> GetDeviceIdAsync(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        string? deviceId = null;
        if (accessTokens.ContainsKey(accessToken))
        {
            deviceId = accessToken[accessTokenPrefix.Length..];
        }
        return Task.FromResult(deviceId);
    }

    public Task<(string userId, string accessToken)?> LogInAsync(string deviceId, string token)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(token);

        if (token == loginToken)
        {
            string accessToken = accessTokenPrefix + deviceId;
            accessTokens.TryAdd(accessToken, null);
            return Task.FromResult<(string userId, string accessToken)?>((userId, accessToken));
        }
        else
        {
            return Task.FromResult<(string userId, string accessToken)?>(default);
        }
    }

    public Task<string?> AuthenticateAsync(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        if (accessTokens.ContainsKey(accessToken))
        {
            return Task.FromResult<string?>(userId);
        }
        else
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task LogOutAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        string accessToken = accessTokenPrefix + deviceId;
        accessTokens.TryRemove(accessToken, out object? _);
        return Task.CompletedTask;
    }

    public Task LogOutAllAsync()
    {
        accessTokens.Clear();
        return Task.CompletedTask;
    }
}
