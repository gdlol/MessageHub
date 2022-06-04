namespace MessageHub.HomeServer;

public interface IIdentity : IDisposable
{
    public string Id { get; }
    public bool IsReadOnly { get; }
    string SignatureAlgorithm { get; }
    string Signature { get; }
    IReadOnlySet<string> SupportedAlgorithms { get; }
    VerifyKeys VerifyKeys { get; }
    ServerKeys GetServerKeys();
    IIdentity AsReadOnly();
    byte[] CreateSignature(string algorithm, string keyName, byte[] data, long timestamp);
}

public interface IIdentityService
{
    bool HasSelfIdentity { get; }
    IIdentity GetSelfIdentity();
    IReadOnlySet<string> SupportedAlgorithms { get; }
    string? Verify(ServerKeys serverKeys);
    bool VerifySignature(string algorithm, string key, byte[] data, byte[] signature);
}
