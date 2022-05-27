using System.Collections.Concurrent;
using System.Web;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

internal class DummyAuthenticator : IAuthenticator
{
    private readonly IPeerIdentity peerIdentity;
    private readonly string loginToken;
    private readonly string userId;
    private readonly string accessTokenPrefix;
    private readonly INetworkProvider networkProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly Notifier<(string, string[])> membershipUpdateNotifier;
    private readonly RoomEventSubscriber roomEventSubscriber;

    public DummyAuthenticator(
        IPeerIdentity peerIdentity,
        INetworkProvider networkProvider,
        ILoggerFactory loggerFactory,
        Notifier<(string, string[])> membershipUpdateNotifier,
        RoomEventSubscriber roomEventSubscriber)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(networkProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);
        ArgumentNullException.ThrowIfNull(roomEventSubscriber);

        this.peerIdentity = peerIdentity;
        this.networkProvider = networkProvider;
        this.loggerFactory = loggerFactory;
        this.membershipUpdateNotifier = membershipUpdateNotifier;
        this.roomEventSubscriber = roomEventSubscriber;
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

    public async Task<(string userId, string accessToken)?> LogInAsync(string deviceId, string token)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(token);

        if (token == loginToken)
        {
            string accessToken = accessTokenPrefix + deviceId;
            if (accessTokens.TryAdd(accessToken, null))
            {
                await networkProvider.InitializeAsync(
                    peerIdentity,
                    loggerFactory,
                    serverKeys =>
                    {
                        if (peerIdentity.Verify(serverKeys))
                        {
                            return new DummyIdentity(isReadOnly: true, id: serverKeys.ServerName);
                        }
                        return null;
                    },
                    roomEventSubscriber.ReceiveEvent,
                    membershipUpdateNotifier);
            }
            return (userId, accessToken);
        }
        else
        {
            return null;
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

    public async Task LogOutAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        string accessToken = accessTokenPrefix + deviceId;
        accessTokens.TryRemove(accessToken, out object? _);
        await networkProvider.ShutdownAsync();
    }

    public async Task LogOutAllAsync()
    {
        accessTokens.Clear();
        await networkProvider.ShutdownAsync();
    }
}
