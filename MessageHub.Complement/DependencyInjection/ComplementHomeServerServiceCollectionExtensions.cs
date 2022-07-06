
using MessageHub.Complement.HomeServer;
using MessageHub.Complement.ReverseProxy;

namespace MessageHub.Complement.DependencyInjection;

public static class ComplementHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddComplementHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.AddHttpForwarder();
        services.AddSingleton<HomeServerHttpForwarder>();
        services.AddSingleton<HomeServerClient>();
        services.AddSingleton<IUserRegistration, UserRegistration>();
        services.AddSingleton<UserProfileProxy>();
        return services;
    }
}
