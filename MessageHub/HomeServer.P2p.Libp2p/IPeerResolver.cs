namespace MessageHub.HomeServer.P2p.Libp2p;

public interface IPeerResolver
{
    Task<string> ResolveAddressInfoAsync(
        string id,
        string? rendezvousPoint = null,
        CancellationToken cancellationToken = default);
}
