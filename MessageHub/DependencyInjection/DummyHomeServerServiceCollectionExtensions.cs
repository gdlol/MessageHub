using MessageHub.HomeServer;
using MessageHub.HomeServer.Dummy;

namespace MessageHub.DependencyInjection;

public static class DummyHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddDummyHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthenticator, DummyAuthenticator>();
        services.AddSingleton<IAccountData, DummyAccountData>();
        services.AddSingleton<IContentRepository, DummyContentRepository>();
        services.AddSingleton<IUserProfile, DummyUserProfile>();
        services.AddSingleton<IRoomLoader, DummyRoomLoader>();
        services.AddSingleton<IEventSender, DummyEventSender>();
        return services;
    }
}
