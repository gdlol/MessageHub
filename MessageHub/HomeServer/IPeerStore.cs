using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer;

public interface IPeerStore
{
    IReadOnlySet<string> PeerIds { get; }
    bool TryGetPeer(string peerId, [NotNullWhen(true)] out IPeerIdentity? peer);
}
