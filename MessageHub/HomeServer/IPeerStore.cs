namespace MessageHub.HomeServer;

public interface IPeerStore
{
    IReadOnlySet<string> PeerIds { get; }
}
