using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer;

public static class PeerIdentityExtensions
{
    public static JsonElement SignJson(this IPeerIdentity identity, JsonElement element)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{nameof(element.ValueKind)}: {element.ValueKind}", nameof(element));
        }
        if (identity.IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(identity.IsReadOnly)}: {identity.IsReadOnly}");
        }

        var signatures = new ServerSignatures();
        JsonNode? unsigned;
        var jsonObject = JsonObject.Create(element);
        if (jsonObject is null)
        {
            throw new InvalidOperationException();
        }
        jsonObject.Remove(nameof(signatures));
        bool hasUnsignedData = jsonObject.Remove(nameof(unsigned), out unsigned);
        var jsonBytes = CanonicalJson.SerializeToBytes(jsonObject);
        foreach (var keyIdentifier in identity.VerifyKeys.Keys.Keys)
        {
            var (algorithm, keyName) = keyIdentifier;
            if (!identity.SupportedAlgorithms.Contains(keyIdentifier.Algorithm))
            {
                continue;
            }
            var signature = identity.CreateSignature(algorithm, keyName, jsonBytes);
            var signatureString = UnpaddedBase64Encoder.Encode(signature);
            signatures[keyIdentifier] = signatureString;
        }
        jsonObject[nameof(signatures)] = JsonObject.Create(
            JsonSerializer.SerializeToElement(new Signatures
            {
                [identity.Id] = signatures
            }));
        if (hasUnsignedData)
        {
            jsonObject[nameof(unsigned)] = unsigned;
        }
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    public static PersistentDataUnit SignEvent(this IPeerIdentity identity, PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(pdu);

        var signedElement = identity.SignJson(pdu.ToJsonElement());
        return signedElement.Deserialize<PersistentDataUnit>()!;
    }

    public static bool VerifyJson(this IPeerIdentity self, string peerId, JsonElement element)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(peerId);
        ArgumentNullException.ThrowIfNull(element);

        // Get signatures mapping.
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        ServerKeys? serverKeys;
        if (!element.TryGetProperty("server_keys", out var serverKeysElement))
        {
            return false;
        }
        try
        {
            serverKeys = serverKeysElement.Deserialize<ServerKeys>();
        }
        catch (Exception)
        {
            return false;
        }
        if (serverKeys is null)
        {
            return false;
        }
        if (serverKeys.ServerName != peerId)
        {
            return false;
        }
        if (!self.Verify(serverKeys))
        {
            return false;
        }
        JsonElement signatures;
        if (!element.TryGetProperty(nameof(signatures), out signatures))
        {
            return false;
        }
        if (signatures.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (!signatures.TryGetProperty(peerId, out var identitySignatures))
        {
            return false;
        }
        if (identitySignatures.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Get supported signatures.
        var supportedSignatures = new List<(string algorithm, string key, byte[] signature)>();
        foreach (var property in identitySignatures.EnumerateObject())
        {
            if (!KeyIdentifier.TryParse(property.Name, out var keyIdentifier))
            {
                continue;
            }
            if (!self.SupportedAlgorithms.Contains(keyIdentifier.Algorithm))
            {
                continue;
            }
            if (!serverKeys.VerifyKeys.TryGetValue(keyIdentifier, out var key))
            {
                continue;
            }
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            string? signature = property.Value.GetString();
            if (string.IsNullOrEmpty(signature))
            {
                continue;
            }
            try
            {
                var signatureBytes = UnpaddedBase64Encoder.DecodeBytes(signature);
                supportedSignatures.Add((keyIdentifier.Algorithm, key, signatureBytes));
            }
            catch (Exception)
            {
                continue;
            }
        }
        if (supportedSignatures.Count == 0)
        {
            return false;
        }

        // Verify signatures.
        var jsonObject = JsonObject.Create(element);
        if (jsonObject is null)
        {
            throw new InvalidOperationException();
        }
        jsonObject.Remove(nameof(signatures));
        jsonObject.Remove("unsigned");
        var jsonBytes = CanonicalJson.SerializeToBytes(jsonObject);
        foreach (var (algorithm, key, signature) in supportedSignatures)
        {
            if (self.VerifySignature(algorithm, key, jsonBytes, signature))
            {
                return true;
            }
        }
        return false;
    }
}
