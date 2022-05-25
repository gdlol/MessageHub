namespace MessageHub.HomeServer;

public interface IPeerIdentity
{
    bool IsReadOnly { get; }
    string Id { get; }
    string SignatureAlgorithm { get; }
    string Signature { get; }
    IReadOnlySet<string> SupportedAlgorithms { get; }
    VerifyKeys VerifyKeys { get; }
    ServerKeys GetServerKeys();
    bool Verify(ServerKeys serverKeys);
    IPeerIdentity AsReadOnly();
    byte[] CreateSignature(string algorithm, string keyName, byte[] data);
    bool VerifySignature(string algorithm, string key, byte[] data, byte[] signature);
}
