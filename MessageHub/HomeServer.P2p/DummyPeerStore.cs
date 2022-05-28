namespace MessageHub.HomeServer.P2p;

public class DummyPeerStore : IPeerStore
{
    public IReadOnlySet<string> PeerIds { get; }

    public DummyPeerStore(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        PeerIds = new HashSet<string>(config.Peers.Keys);
    }
}
