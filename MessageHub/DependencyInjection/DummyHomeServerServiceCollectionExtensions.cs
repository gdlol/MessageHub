using MessageHub.HomeServer;
using MessageHub.HomeServer.Dummy;

namespace MessageHub.DependencyInjection;

public static class DummyHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddDummyHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthenticator, DummyAuthenticator>();
        services.AddSingleton<IPersistenceService, DummyPersistenceService>();
        services.AddSingleton<IRoomLoader, DummyRoomLoader>();
        return services;
    }
}
