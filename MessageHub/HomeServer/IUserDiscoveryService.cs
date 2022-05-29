namespace MessageHub.HomeServer;

public interface IUserDiscoveryService
{
    Task<IPeerIdentity[]> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken);
}
