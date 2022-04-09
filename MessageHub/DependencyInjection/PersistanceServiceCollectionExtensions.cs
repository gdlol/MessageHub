using MessageHub.Persistence;

namespace MessageHub.DependencyInjection;

public static class PersistanceServiceCollectionExtensions
{
    public static IServiceCollection AddMatrixPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new MatrixPersistenceService());
        return services;
    }
}
