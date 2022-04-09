using MessageHub.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageHub.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddMatrixAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthenticationCore(options =>
        {
            options.DefaultScheme = MatrixDefaults.AuthenticationScheme;
        });
        services.AddWebEncoders();
        services.TryAddSingleton<ISystemClock, SystemClock>();
        var builder = new AuthenticationBuilder(services);
        builder.AddScheme<MatrixAuthenticationSchemeOptions, MatrixAuthenticationHandler>(
            MatrixDefaults.AuthenticationScheme,
            _ => { });
        return services;
    }
}
