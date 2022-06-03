using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class UserDiscoveryService : IUserDiscoveryService
{
    private readonly IIdentityService identityService;
    private readonly INetworkProvider networkProvider;

    public UserDiscoveryService(IIdentityService identityService, INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(networkProvider);

        this.identityService = identityService;
        this.networkProvider = networkProvider;
    }

    public Task<IIdentity[]> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken)
    {
        var identity = identityService.GetSelfIdentity();
        return networkProvider.SearchPeersAsync(identity, searchTerm, cancellationToken);
    }
}
