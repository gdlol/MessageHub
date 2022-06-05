namespace MessageHub.HomeServer.P2p.Libp2p;

public static class AuthorizedPeer
{
    public static KeyIdentifier KeyIdentifier { get; } = new KeyIdentifier("libp2p", "PeerID");

    public static bool Verify(IIdentity identity, string peerId)
    {
        return identity.VerifyKeys.Keys.TryGetValue(KeyIdentifier, out var authorizedPeerId)
            && authorizedPeerId == peerId;
    }
}
