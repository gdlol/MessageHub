using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer.Dummy;

public class DummyPeerStore : IPeerStore
{
    public IReadOnlySet<string> PeerIds { get; }

    private readonly Dictionary<string, IPeerIdentity> peers;

    public DummyPeerStore(IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);

        PeerIds = new HashSet<string>
        {
            peerIdentity.Id
        };
        peers = new Dictionary<string, IPeerIdentity>
        {
            [peerIdentity.Id] = peerIdentity
        };
    }

    public bool TryGetPeer(string peerId, [NotNullWhen(true)] out IPeerIdentity? peer)
    {
        return peers.TryGetValue(peerId, out peer);
    }
}
