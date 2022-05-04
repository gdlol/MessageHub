using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer.Dummy;

public class DummyPeerStore : IPeerStore
{
    public IReadOnlySet<string> PeerIds { get; }

    private readonly Dictionary<string, IPeerIdentity> peers;

    public DummyPeerStore(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        PeerIds = new HashSet<string>(config.Peers.Keys);
        peers = new Dictionary<string, IPeerIdentity>();
        foreach (string peerId in config.Peers.Keys)
        {
            peers[peerId] = new DummyIdentity(peerId != config.PeerId, peerId);
        }
    }

    public bool TryGetPeer(string peerId, [NotNullWhen(true)] out IPeerIdentity? peer)
    {
        return peers.TryGetValue(peerId, out peer);
    }
}
