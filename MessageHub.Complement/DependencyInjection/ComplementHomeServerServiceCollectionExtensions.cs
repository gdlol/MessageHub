using MessageHub.Complement.Authentication;
using MessageHub.Complement.HomeServer;
using MessageHub.Complement.ReverseProxy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageHub.Complement.DependencyInjection;

public static class ComplementHomeServerServiceCollectionExtensions
{
    public static IServiceCollection AddComplementAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthenticationCore();
        services.AddWebEncoders();
        services.TryAddSingleton<ISystemClock, SystemClock>();
        var builder = new AuthenticationBuilder(services);
        builder.AddScheme<ComplementAuthenticationSchemeOptions, ComplementAuthenticationHandler>(
            ComplementAuthenticationSchemes.Complement,
            _ => { });
        return services;
    }

    public static IServiceCollection AddComplementHomeServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.AddHttpForwarder();
        services.AddSingleton<HomeServerHttpForwarder>();
        services.AddSingleton<HomeServerClient>();
        services.AddSingleton<FillJsonContentType>();
        services.AddSingleton<UserProfileProxy>();
        services.AddSingleton<ContentRepositoryProxy>();
        services.AddSingleton<IUserLogIn, UserLogIn>();
        services.AddSingleton<IUserRegistration, UserRegistration>();
        return services;
    }
}
