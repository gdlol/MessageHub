namespace MessageHub.HomeServer;

public interface IUserDiscoveryService
{
    Task<IEnumerable<IIdentity>> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken);
    Task<(string? avatarUrl, string? displayName)> GetUserProfileAsync(
        string userId,
        CancellationToken cancellationToken);
}
