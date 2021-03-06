using System.Text.Json.Nodes;
using MessageHub.HomeServer.Formatting;
using MessageHub.HomeServer.P2p.Libp2p;
using MessageHub.Serialization;
using NSec.Cryptography;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

public class LocalIdentityService : IIdentityService
{
    private readonly object locker = new();
    private LocalIdentity? selfIdentity;

    public bool HasSelfIdentity => selfIdentity is not null;

    public IReadOnlySet<string> SupportedAlgorithms => new HashSet<string> { LocalIdentity.AlgorithmName };

    public IIdentity GetSelfIdentity()
    {
        return selfIdentity ?? throw new InvalidOperationException();
    }

    public void UpdateSelfIdentity(Func<LocalIdentity?, LocalIdentity> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (locker)
        {
            var identity = selfIdentity;
            selfIdentity = update(identity);
            if (identity != selfIdentity)
            {
                identity?.Dispose();
            }
        }
    }

    public string? Verify(ServerKeys serverKeys)
    {
        if (!serverKeys.Signatures.TryGetValue(serverKeys.ServerName, out var signatures))
        {
            return null;
        }
        if (!signatures.TryGetValue(LocalIdentity.ServerKeyIdentifier, out string? signature))
        {
            return null;
        }
        var publicKeyBlob = UnpaddedBase64Encoder.DecodeBytes(serverKeys.ServerName);
        var signatureAlgorithm = SignatureAlgorithm.Ed25519;
        if (!PublicKey.TryImport(signatureAlgorithm, publicKeyBlob, KeyBlobFormat.RawPublicKey, out var publicKey))
        {
            return null;
        }
        var signatureData = UnpaddedBase64Encoder.DecodeBytes(signature);
        var element = DefaultJsonSerializer.SerializeToElement(serverKeys);
        var jsonObject = JsonObject.Create(element);
        if (jsonObject is null)
        {
            throw new InvalidOperationException();
        }
        jsonObject.Remove(nameof(signatures));
        var data = CanonicalJson.SerializeToBytes(jsonObject);
        if (!signatureAlgorithm.Verify(publicKey!, data, signatureData))
        {
            return null;
        }
        return CidEncoding.EncodeEd25519PublicKey(publicKeyBlob);
    }

    public bool VerifySignature(string algorithm, string key, byte[] data, byte[] signature)
    {
        if (!SupportedAlgorithms.Contains(algorithm))
        {
            throw new NotSupportedException($"{nameof(algorithm)}: {algorithm}");
        }
        var publicKeyBlob = UnpaddedBase64Encoder.DecodeBytes(key);
        var signatureAlgorithm = SignatureAlgorithm.Ed25519;
        if (!PublicKey.TryImport(signatureAlgorithm, publicKeyBlob, KeyBlobFormat.RawPublicKey, out var publicKey))
        {
            throw new ArgumentException(key, nameof(key));
        }
        return signatureAlgorithm.Verify(publicKey!, data, signature);
    }
}
