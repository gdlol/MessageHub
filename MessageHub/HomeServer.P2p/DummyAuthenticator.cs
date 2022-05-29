using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Web;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p;

internal class DummyAuthenticator : IAuthenticator
{
    private readonly Config config;
    private readonly string loginToken;
    private readonly string userId;
    private readonly string accessTokenPrefix;
    private readonly INetworkProvider networkProvider;
    private readonly IUserProfile userProfile;
    private readonly ILoggerFactory loggerFactory;
    private readonly IMemoryCache memoryCache;
    private readonly Notifier<(string, string[])> membershipUpdateNotifier;
    private readonly ITimelineLoader timelineLoader;
    private readonly IRooms rooms;
    private readonly RoomEventSubscriber roomEventSubscriber;
    private readonly string selfUrl;

    public DummyAuthenticator(
        Config config,
        INetworkProvider networkProvider,
        IUserProfile userProfile,
        ILoggerFactory loggerFactory,
        IMemoryCache memoryCache,
        Notifier<(string, string[])> membershipUpdateNotifier,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        RoomEventSubscriber roomEventSubscriber,
        IServer server)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(networkProvider);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(roomEventSubscriber);
        ArgumentNullException.ThrowIfNull(server);

        this.config = config;
        this.networkProvider = networkProvider;
        this.userProfile = userProfile;
        this.loggerFactory = loggerFactory;
        this.memoryCache = memoryCache;
        this.membershipUpdateNotifier = membershipUpdateNotifier;
        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
        this.roomEventSubscriber = roomEventSubscriber;
        loginToken = config.PeerId;
        accessTokenPrefix = config.PeerId;
        userId = UserIdentifier.FromId(config.PeerId).ToString();
        selfUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
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
            if (DummyIdentity.Self is null)
            {
                var (keyId, networkKey) = networkProvider.GetVerifyKey();
                DummyIdentity.Self = new DummyIdentity(
                    false,
                    config.PeerId,
                    new VerifyKeys(new Dictionary<KeyIdentifier, string>
                    {
                        [keyId] = networkKey,
                    }.ToImmutableDictionary(),
                    DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()));
            }

            string accessToken = accessTokenPrefix + deviceId;
            if (accessTokens.TryAdd(accessToken, null))
            {
                var uri = new Uri(selfUrl);
                await networkProvider.InitializeAsync(
                    DummyIdentity.Self,
                    userProfile,
                    loggerFactory,
                    memoryCache,
                    serverKeys =>
                    {
                        if (DummyIdentity.Self.Verify(serverKeys))
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
                    },
                    roomEventSubscriber.ReceiveEvent,
                    membershipUpdateNotifier,
                    timelineLoader,
                    rooms,
                    $"{uri.Host}:{uri.Port}");
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
