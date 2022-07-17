using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Formatting;
using MessageHub.HomeServer.P2p.Libp2p;
using MessageHub.Serialization;
using NSec.Cryptography;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

public sealed class LocalIdentity : IIdentity
{
    public const string AlgorithmName = "ed25519";
    public const string KeyName = "local";
    public readonly static KeyIdentifier ServerKeyIdentifier = new(AlgorithmName, "ID");
    public readonly static KeyIdentifier VerifyKeyIdentifier = new(AlgorithmName, KeyName);

    private readonly static SignatureAlgorithm signatureAlgorithm = NSec.Cryptography.SignatureAlgorithm.Ed25519;

    public string Id { get; }

    public bool IsReadOnly => key is null;

    public string SignatureAlgorithm => AlgorithmName;

    public IReadOnlySet<string> SupportedAlgorithms => new HashSet<string> { AlgorithmName };

    public string Signature { get; }

    public VerifyKeys VerifyKeys { get; }

    private readonly Key? key;
    private readonly JsonElement serverKeys;
    private readonly long validUntilTimestamp;

    private LocalIdentity(Key? key, ServerKeys serverKeys)
    {
        this.key = key;
        this.serverKeys = DefaultJsonSerializer.SerializeToElement(serverKeys);
        validUntilTimestamp = serverKeys.ValidUntilTimestamp;
        var publicKeyBlob = UnpaddedBase64Encoder.DecodeBytes(serverKeys.ServerName);
        Id = CidEncoding.EncodeEd25519PublicKey(publicKeyBlob);
        Signature = serverKeys.Signatures[serverKeys.ServerName][ServerKeyIdentifier];
        VerifyKeys = new VerifyKeys(serverKeys.VerifyKeys.ToImmutableDictionary(), serverKeys.ValidUntilTimestamp);
    }

    // Create verify key, sign identity using server key.
    public static LocalIdentity Create(Key serverKey, VerifyKeys verifyKeys)
    {
        var verifyKey = Key.Create(signatureAlgorithm);
        verifyKeys = new VerifyKeys(
            verifyKeys.Keys.SetItem(
                VerifyKeyIdentifier,
                UnpaddedBase64Encoder.Encode(verifyKey.PublicKey.Export(KeyBlobFormat.RawPublicKey))),
            verifyKeys.ExpireTimestamp);
        var serverKeys = new ServerKeys
        {
            ServerName = UnpaddedBase64Encoder.Encode(serverKey.PublicKey.Export(KeyBlobFormat.RawPublicKey)),
            ValidUntilTimestamp = verifyKeys.ExpireTimestamp,
            VerifyKeys = verifyKeys.Keys.ToDictionary(x => x.Key, x => x.Value)
        };
        var element = DefaultJsonSerializer.SerializeToElement(serverKeys);
        var data = CanonicalJson.SerializeToBytes(element);
        var signatureData = signatureAlgorithm.Sign(serverKey, data);
        serverKeys.Signatures = new Signatures
        {
            [serverKeys.ServerName] = new ServerSignatures
            {
                [ServerKeyIdentifier] = UnpaddedBase64Encoder.Encode(signatureData)
            }
        };
        return new LocalIdentity(verifyKey, serverKeys);
    }

    // Read only identity.
    public static LocalIdentity Create(ServerKeys serverKeys)
    {
        return new LocalIdentity(null, serverKeys);
    }

    public void Dispose()
    {
        key?.Dispose();
    }

    public ServerKeys GetServerKeys()
    {
        return serverKeys.Deserialize<ServerKeys>()!;
    }

    public IIdentity AsReadOnly()
    {
        if (key is null)
        {
            return this;
        }
        return new LocalIdentity(null, GetServerKeys());
    }

    public byte[] CreateSignature(string algorithm, string keyName, byte[] data, long timestamp)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentNullException.ThrowIfNull(keyName);
        ArgumentNullException.ThrowIfNull(data);

        if (key is null)
        {
            throw new InvalidOperationException();
        }
        if (!SupportedAlgorithms.Contains(algorithm))
        {
            throw new NotSupportedException($"{nameof(algorithm)}: {algorithm}");
        }
        if (keyName != KeyName)
        {
            throw new ArgumentException(keyName, nameof(keyName));
        }
        if (timestamp > validUntilTimestamp)
        {
            throw new InvalidOperationException($"{nameof(timestamp)}: {timestamp} > {validUntilTimestamp}");
        }
        return NSec.Cryptography.SignatureAlgorithm.Ed25519.Sign(key, data);
    }
}
