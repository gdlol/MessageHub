namespace MessageHub.HomeServer.P2p.Libp2p;

public static class AuthenticatedPeer
{
    public static KeyIdentifier KeyIdentifier { get; } = new KeyIdentifier("libp2p", "PeerID");
}
