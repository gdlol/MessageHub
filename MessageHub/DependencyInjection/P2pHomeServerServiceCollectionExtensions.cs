using MessageHub.HomeServer;
using MessageHub.HomeServer.P2p;
using MessageHub.HomeServer.P2p.FasterKV;
using MessageHub.HomeServer.P2p.Libp2p;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.P2p.Remote;
using MessageHub.HomeServer.P2p.Rooms;
using MessageHub.HomeServer.P2p.Rooms.Timeline;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.DependencyInjection;

public static class P2pHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddP2pHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            return new FasterStorageConfig
            {
                DataPath = config.DataPath
            };
        });
        services.AddSingleton<IStorageProvider, FasterStorageProvider>();
        services.AddSingleton<Notifier<(string, string[])>>();
        services.AddSingleton<INetworkProvider>(
            new Libp2pNetworkProvider(
                new HostConfig
                {
                    AdvertisePrivateAddresses = true
                },
                new DHTConfig()));
        services.AddSingleton<IAccountData, AccountData>();
        services.AddSingleton<RoomEventSubscriber>();
        services.AddSingleton<IAuthenticator, DummyAuthenticator>();
        services.AddSingleton<IContentRepository, ContentRepository>();
        services.AddSingleton<IRoomDiscoveryService, RoomDiscoveryService>();
        services.AddSingleton<IUserDiscoveryService, UserDiscoveryService>();
        services.AddSingleton<IUserProfile, UserProfile>();
        services.AddSingleton<IRequestHandler, RequestHandler>();
        services.AddSingleton<IRemoteContentRepository, RemoteContentRepository>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        services.AddScoped<IPeerIdentity>(_ => DummyIdentity.Self ?? throw new InvalidOperationException());
        services.AddTransient(provider =>
        {
            var storageProvider = provider.GetRequiredService<IStorageProvider>();
            return EventStore.Instance ??
                EventStore.CreateAsync(storageProvider.GetEventStore()).AsTask().GetAwaiter().GetResult();
        });
        services.AddTransient<IRooms, Rooms>();
        services.AddTransient<IEventSaver, EventSaver>();
        services.AddTransient<ITimelineLoader, TimelineLoader>();
        services.AddTransient<IEventReceiver, EventReceiver>();
        services.AddTransient<IRemoteRooms, RemoteRooms>();
        return services;
    }
}
