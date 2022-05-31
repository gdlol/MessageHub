namespace MessageHub.HomeServer;

public interface IUserDiscoveryService
{
    Task<IIdentity[]> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken);
}
