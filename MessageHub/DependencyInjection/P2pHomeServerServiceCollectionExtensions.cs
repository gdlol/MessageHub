using MessageHub.HomeServer;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.P2p;
using MessageHub.HomeServer.P2p.FasterKV;
using MessageHub.HomeServer.P2p.Libp2p;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.P2p.Libp2p.Services;
using MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;
using MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;
using MessageHub.HomeServer.P2p.Libp2p.Services.Logging;
using MessageHub.HomeServer.P2p.Libp2p.Services.Membership;
using MessageHub.HomeServer.P2p.Libp2p.Services.Presence;
using MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;
using MessageHub.HomeServer.P2p.LocalIdentity;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.P2p.Remote;
using MessageHub.HomeServer.P2p.Rooms;
using MessageHub.HomeServer.P2p.Rooms.Timeline;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.HomeServer.Services;

namespace MessageHub.DependencyInjection;

public static class P2pHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddFasterKV(this IServiceCollection services, string dataPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataPath);

        services.AddSingleton(new FasterStorageConfig
        {
            DataPath = dataPath
        });
        services.AddSingleton<IStorageProvider, FasterStorageProvider>();
        return services;
    }

    public static IServiceCollection AddLibp2p(
        this IServiceCollection services,
        HostConfig hostConfig,
        DHTConfig dhtConfig)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(hostConfig);
        ArgumentNullException.ThrowIfNull(dhtConfig);

        services.AddSingleton(hostConfig);
        services.AddSingleton(dhtConfig);
        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddSingleton<PublishEventNotifier>();
        services.AddSingleton<TopicMemberUpdateNotifier>();
        services.AddSingleton<LoggingServiceContext>();
        services.AddSingleton<LoggingService>();
        services.AddSingleton<HttpProxyService.Context>();
        services.AddSingleton<HttpProxyService>();
        services.AddSingleton<AddressCachingService.Context>();
        services.AddSingleton<AddressCachingService>();
        services.AddSingleton<MdnsBackgroundService.Context>();
        services.AddSingleton<MdnsBackgroundService>();
        services.AddSingleton<AdvertisingServiceContext>();
        services.AddSingleton<AdvertisingService>();
        services.AddSingleton<RelayDiscoveryService.Context>();
        services.AddSingleton<RelayDiscoveryService>();
        services.AddSingleton<PubSubServiceContext>();
        services.AddSingleton<PubSubService>();
        services.AddSingleton<MembershipServiceContext>();
        services.AddSingleton<MembershipService>();
        services.AddSingleton<BackfillingServiceContext>();
        services.AddSingleton<BackfillingService>();
        services.AddSingleton<PresenceServiceContext>();
        services.AddSingleton<PresenceService>();
        services.AddSingleton<INetworkProvider, Libp2pNetworkProvider>();
        return services;
    }

    public static IServiceCollection AddLocalIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LocalIdentityService>();
        services.AddSingleton<LocalAuthenticator>();
        services.AddSingleton<KeyRotationService>();
        services.AddHostedService<HostedKeyRotationService>();
        services.AddSingleton<IIdentityService>(provider => provider.GetRequiredService<LocalIdentityService>());
        services.AddSingleton<IAuthenticator>(provider => provider.GetRequiredService<LocalAuthenticator>());
        return services;
    }

    public static IServiceCollection AddP2pHomeServer(this IServiceCollection services)
    {
        services.AddSingleton<TimelineUpdateNotifier>();
        services.AddSingleton<AuthenticatedRequestNotifier>();
        services.AddSingleton<UserProfileUpdateNotifier>();
        services.AddSingleton<PresenceUpdateNotifier>();
        services.AddSingleton<UnresolvedEventNotifier>();
        services.AddSingleton<MembershipUpdateNotifier>();
        services.AddSingleton<RemoteRequestNotifier>();
        services.AddSingleton<IAccountData, AccountData>();
        services.AddSingleton<IContentRepository, ContentRepository>();
        services.AddSingleton<IEventReceiver, EventReceiver>();
        services.AddSingleton<IRoomDiscoveryService, RoomDiscoveryService>();
        services.AddSingleton<IUserDiscoveryService, UserDiscoveryService>();
        services.AddSingleton<IUserProfile, UserProfile>();
        services.AddSingleton<IUserPresence, UserPresence>();
        services.AddSingleton<IUserReceipts, UserReceipts>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        services.AddSingleton<IRemoteContentRepository, RemoteContentRepository>();
        services.AddSingleton<IRemoteRooms, RemoteRooms>();
        services.AddSingleton<IRequestHandler, RequestHandler>();
        services.AddSingleton(provider =>
        {
            var storageProvider = provider.GetRequiredService<IStorageProvider>();
            return EventStore.Instance ??
                EventStore.CreateAsync(storageProvider.GetEventStore()).AsTask().GetAwaiter().GetResult();
        });
        services.AddSingleton<IRooms, Rooms>();
        services.AddSingleton<IEventSaver, EventSaver>();
        services.AddSingleton<ITimelineLoader, TimelineLoader>();
        services.AddSingleton<ProfileUpdateService>();
        services.AddHostedService<HostedProfileUpdateService>();
        return services;
    }
}
