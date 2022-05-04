using MessageHub.HomeServer;
using MessageHub.HomeServer.Dummy;
using MessageHub.HomeServer.Dummy.Remote;
using MessageHub.HomeServer.Dummy.Rooms;
using MessageHub.HomeServer.Dummy.Rooms.Timeline;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.DependencyInjection;

public static class DummyHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddDummyHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAccountData, DummyAccountData>();
        services.AddSingleton<IAuthenticator, DummyAuthenticator>();
        services.AddSingleton<IContentRepository, DummyContentRepository>();
        services.AddSingleton<IPeerIdentity, DummyIdentity>();
        services.AddSingleton<IPeerStore, DummyPeerStore>();
        services.AddSingleton<IRoomDiscoveryService, DummyRoomDiscoveryService>();
        services.AddSingleton<IUserProfile, DummyUserProfile>();
        services.AddSingleton<IRequestHandler, DummyRequestHandler>();
        services.AddHttpClient<DummyRequestHandler>();
        services.AddSingleton<IRemoteContentRepository, DummyRemoteContentRepository>();
        services.AddSingleton<IEventPublisher, DummyEventPublisher>();
        services.AddSingleton<IRooms, DummyRooms>();
        services.AddSingleton<IEventSaver, DummyEventSaver>();
        services.AddSingleton<ITimelineLoader, DummyTimelineLoader>();
        services.AddSingleton<IEventReceiver, EventReceiver>();
        services.AddSingleton<IRemoteRooms, RemoteRooms>();
        return services;
    }
}
