using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public static class CidEncoding
{
    public static string EncodeEd25519PublicKey(byte[] publicKey)
    {
        using var hexPublicKey = StringHandle.FromString(Convert.ToHexString(publicKey));
        using var error = NativeMethods.EncodeEd25519PublicKey(hexPublicKey, out var result);
        LibP2pException.Check(error);
        using var _ = result;
        return result.ToString();
    }
}
