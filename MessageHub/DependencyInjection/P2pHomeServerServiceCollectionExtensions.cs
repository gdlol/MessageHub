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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageHub.DependencyInjection;

public static class P2pHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddFasterKV(this IServiceCollection services, string dataPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataPath);

        services.TryAddSingleton(new FasterStorageConfig
        {
            DataPath = dataPath
        });
        services.TryAddSingleton<IStorageProvider, FasterStorageProvider>();
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

        services.TryAddSingleton(hostConfig);
        services.TryAddSingleton(dhtConfig);
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
        services.TryAddSingleton<INetworkProvider, Libp2pNetworkProvider>();
        return services;
    }

    public static IServiceCollection AddLocalIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LocalIdentityService>();
        services.AddSingleton<LocalAuthenticator>();
        services.AddSingleton<KeyRotationService>();
        services.AddHostedService<HostedKeyRotationService>();
        services.TryAddSingleton<IIdentityService>(provider => provider.GetRequiredService<LocalIdentityService>());
        services.TryAddSingleton<IAuthenticator>(provider => provider.GetRequiredService<LocalAuthenticator>());
        return services;
    }

    public static IServiceCollection AddP2pHomeServer(this IServiceCollection services)
    {        
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TimelineUpdateNotifier>();
        services.AddSingleton<AuthenticatedRequestNotifier>();
        services.AddSingleton<UserProfileUpdateNotifier>();
        services.AddSingleton<PresenceUpdateNotifier>();
        services.AddSingleton<UnresolvedEventNotifier>();
        services.AddSingleton<MembershipUpdateNotifier>();
        services.AddSingleton<RemoteRequestNotifier>();
        services.TryAddSingleton<IAccountData, AccountData>();
        services.TryAddSingleton<IContentRepository, ContentRepository>();
        services.TryAddSingleton<IEventReceiver, EventReceiver>();
        services.TryAddSingleton<IRoomDiscoveryService, RoomDiscoveryService>();
        services.TryAddSingleton<IUserDiscoveryService, UserDiscoveryService>();
        services.TryAddSingleton<IUserProfile, UserProfile>();
        services.TryAddSingleton<IUserPresence, UserPresence>();
        services.TryAddSingleton<IUserReceipts, UserReceipts>();
        services.TryAddSingleton<IEventPublisher, EventPublisher>();
        services.TryAddSingleton<IRemoteContentRepository, RemoteContentRepository>();
        services.TryAddSingleton<IRemoteRooms, RemoteRooms>();
        services.TryAddSingleton<IRequestHandler, RequestHandler>();
        services.AddSingleton<EventStore>();
        services.TryAddSingleton<IRooms, Rooms>();
        services.TryAddSingleton<IEventSaver, EventSaver>();
        services.TryAddSingleton<ITimelineLoader, TimelineLoader>();
        services.AddSingleton<ProfileUpdateService>();
        services.AddHostedService<HostedProfileUpdateService>();
        return services;
    }
}
