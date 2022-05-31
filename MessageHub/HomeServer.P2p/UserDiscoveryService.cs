using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class UserDiscoveryService : IUserDiscoveryService
{
    private readonly INetworkProvider networkProvider;

    public UserDiscoveryService(INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(networkProvider);

        this.networkProvider = networkProvider;
    }

    public Task<IIdentity[]> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken)
    {
        return networkProvider.SearchPeersAsync(searchTerm, cancellationToken);
    }
}
