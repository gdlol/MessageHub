using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Web;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

internal class DummyAuthenticator : IAuthenticator
{
    private readonly Config config;
    private readonly string loginToken;
    private readonly string userId;
    private readonly string accessTokenPrefix;
    private readonly DummyIdentityService dummyIdentityService;
    private readonly INetworkProvider networkProvider;

    public DummyAuthenticator(
        Config config,
        DummyIdentityService dummyIdentityService,
        INetworkProvider networkProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(dummyIdentityService);
        ArgumentNullException.ThrowIfNull(networkProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.config = config;
        this.dummyIdentityService = dummyIdentityService;
        this.networkProvider = networkProvider;
        loginToken = config.Id;
        accessTokenPrefix = config.Id;
        userId = UserIdentifier.FromId(config.Id).ToString();
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

        (string, string)? result = null;
        if (token == loginToken)
        {
            lock (dummyIdentityService)
            {
                if (!dummyIdentityService.HasSelfIdentity)
                {
                    var (keyId, networkKey) = networkProvider.GetVerifyKey();
                    var identity = new DummyIdentity(
                        false,
                        config.Id,
                        new VerifyKeys(new Dictionary<KeyIdentifier, string>
                        {
                            [keyId] = networkKey,
                        }.ToImmutableDictionary(),
                        DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()));
                    dummyIdentityService.SetSelfIdentity(identity);
                }
            }

            string accessToken = accessTokenPrefix + deviceId;
            if (accessTokens.TryAdd(accessToken, null))
            {
                lock (networkProvider)
                {
                    networkProvider.Initialize(serverKeys =>
                    {
                        if (dummyIdentityService.Verify(serverKeys))
                        {
                            var verifyKeys = new VerifyKeys(
                                serverKeys.VerifyKeys.ToImmutableDictionary(),
                                serverKeys.ValidUntilTimestamp);
                            return new DummyIdentity(
                                isReadOnly: true,
                                id: serverKeys.ServerName,
                                verifyKeys: verifyKeys);
                        }
                        return null;
                    });
                }
            }
            result = (userId, accessToken);
        }
        return Task.FromResult(result);
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
