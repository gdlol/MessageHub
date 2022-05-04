using MessageHub.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageHub.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddMatrixAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthenticationCore();
        services.AddWebEncoders();
        services.TryAddSingleton<ISystemClock, SystemClock>();
        var builder = new AuthenticationBuilder(services);
        builder.AddScheme<ClientAuthenticationSchemeOptions, ClientAuthenticationHandler>(
            MatrixAuthenticationSchemes.Client,
            _ => { });
        builder.AddScheme<FederationAuthenticationSchemeOptions, FederationAuthenticationHandler>(
            MatrixAuthenticationSchemes.Federation,
            _ => { });
        return services;
    }
}
