using System.Text.Json;
using MessageHub.Federation;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class UserDiscoveryService : IUserDiscoveryService
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly INetworkProvider networkProvider;

    public UserDiscoveryService(
        ILogger<UserDiscoveryService> logger,
        IIdentityService identityService,
        INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(networkProvider);

        this.logger = logger;
        this.identityService = identityService;
        this.networkProvider = networkProvider;
    }

    public Task<IIdentity[]> SearchUsersAsync(string searchTerm, CancellationToken cancellationToken)
    {
        var identity = identityService.GetSelfIdentity();
        return networkProvider.SearchPeersAsync(identity, searchTerm, cancellationToken);
    }

    public async Task<(string? avatarUrl, string? displayName)> GetUserProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var identity = identityService.GetSelfIdentity();
        var request = identity.SignRequest(
            destination: UserIdentifier.Parse(userId).Id,
            requestMethod: HttpMethods.Get,
            requestTarget: $"/_matrix/client/v3/profile/{userId}");
        var result = await networkProvider.SendAsync(request, cancellationToken);
        logger.LogDebug("Get user profile from {}: {}", request.Destination, result);
        string? avatarUrl = null;
        string? displayName = null;
        if (result.TryGetProperty("avatar_url", out var value) && value.ValueKind == JsonValueKind.String)
        {
            avatarUrl = value.GetString();
        }
        if (result.TryGetProperty("displayname", out value) && value.ValueKind == JsonValueKind.String)
        {
            displayName = value.GetString();
        }
        return (avatarUrl, displayName);
    }
}
