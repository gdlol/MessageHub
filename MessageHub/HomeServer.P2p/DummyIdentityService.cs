using System.Security.Cryptography;
using System.Text;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.P2p;

public sealed class DummyIdentity : IIdentity
{
    public string Id { get; }

    public bool IsReadOnly { get; }

    public string SignatureAlgorithm { get; } = "dummy";

    public string Signature => $"dummy-{Id}";

    public IReadOnlySet<string> SupportedAlgorithms { get; } = new HashSet<string> { "dummy" };

    public VerifyKeys VerifyKeys { get; }

    public DummyIdentity(bool isReadOnly, string id, VerifyKeys verifyKeys)
    {
        IsReadOnly = isReadOnly;
        Id = id;
        VerifyKeys = new VerifyKeys(
            verifyKeys.Keys.SetItem(new KeyIdentifier("dummy", "dummy"), $"dummy-key-{id}"),
            verifyKeys.ExpireTimestamp);
    }

    public ServerKeys GetServerKeys()
    {
        return new ServerKeys
        {
            ServerName = Id,
            Signatures = new Signatures
            {
                [Id] = new ServerSignatures
                {
                    [new KeyIdentifier(SignatureAlgorithm, Id)] = Signature
                }
            },
            ValidUntilTimestamp = VerifyKeys.ExpireTimestamp,
            VerifyKeys = VerifyKeys.Keys.ToDictionary(x => x.Key, x => x.Value)
        };
    }

    public IIdentity AsReadOnly()
    {
        return new DummyIdentity(true, Id, VerifyKeys);
    }

    public byte[] CreateSignature(string algorithm, string keyName, byte[] data)
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException();
        }
        if (!VerifyKeys.Keys.ContainsKey(new KeyIdentifier(algorithm, keyName)))
        {
            throw new InvalidOperationException();
        }
        var key = Encoding.UTF8.GetBytes($"dummy-key-{Id}-private");
        var hash = SHA256.HashData(data);
        var signature = Convert.ToHexString(key.Concat(hash).ToArray());
        return Convert.FromHexString(signature);
    }

    public void Dispose() { }
}

public class DummyIdentityService : IIdentityService
{
    private DummyIdentity? selfIdentity;

    public bool HasSelfIdentity => selfIdentity is not null;

    public IReadOnlySet<string> SupportedAlgorithms { get; } = new HashSet<string> { "dummy" };

    public IIdentity GetSelfIdentity()
    {
        return selfIdentity ?? throw new InvalidOperationException();
    }

    public void SetSelfIdentity(DummyIdentity? identity)
    {
        selfIdentity = identity;
    }

    public bool Verify(ServerKeys serverKeys)
    {
        return serverKeys.Signatures.TryGetValue(serverKeys.ServerName, out var signatures)
            && signatures.ContainsValue($"dummy-{serverKeys.ServerName}");
    }

    public bool VerifySignature(string algorithm, string key, byte[] data, byte[] signature)
    {
        if (!SupportedAlgorithms.Contains(algorithm))
        {
            throw new InvalidOperationException();
        }
        var hash = SHA256.HashData(data);
        string signatureString = Convert.ToHexString(signature);
        var keyBytes = Encoding.UTF8.GetBytes($"{key}-private");
        return signatureString == Convert.ToHexString(keyBytes.Concat(hash).ToArray());
    }
}
