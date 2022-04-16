using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer;

public interface IServerIdentity
{
    string ServerKey { get; }
    string Signature { get; }
    IReadOnlySet<string> SupportedAlgorithms { get; }
    VerifyKeys VerifyKeys { get; }
    IReadOnlyList<VerifyKeys> ExpiredKeys { get; }
    byte[] CreateSignature(string algorithm, string keyName, byte[] data);
    bool VerifySignature(string algorithm, string key, byte[] data, byte[] signature);

    public JsonElement SignJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{nameof(element.ValueKind)}: {element.ValueKind}");
        }

        var signatures = new Dictionary<string, string>();
        JsonNode? unsigned;
        var jsonObject = JsonObject.Create(element);
        if (jsonObject is null)
        {
            throw new InvalidOperationException();
        }
        jsonObject.Remove(nameof(signatures));
        bool hasUnsignedData = jsonObject.Remove(nameof(unsigned), out unsigned);
        var jsonBytes = CanonicalJson.SerializeToBytes(jsonObject);
        foreach (var (algorithm, keyName) in VerifyKeys.Keys.Keys)
        {
            var signature = CreateSignature(algorithm, keyName, jsonBytes);
            var signatureString = UnpaddedBase64Encoder.Encode(signature);
            signatures[$"{algorithm}:{keyName}"] = signatureString;
        }
        jsonObject[nameof(signatures)] = JsonObject.Create(
            JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [ServerKey] = signatures
            }));
        if (hasUnsignedData)
        {
            jsonObject[nameof(unsigned)] = unsigned;
        }
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    public bool VerifyJson(JsonElement element, IServerIdentity identity)
    {
        // Get signatures mapping.
        if (element.ValueKind != JsonValueKind.Object)
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
        if (!signatures.TryGetProperty(identity.ServerKey, out var identitySignatures))
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
            string keyIdentifier = property.Name;
            var parts = keyIdentifier.Split(":");
            if (parts.Length != 2)
            {
                continue;
            }
            var (algorithm, keyName) = (parts[0], parts[1]);
            if (!SupportedAlgorithms.Contains(algorithm))
            {
                continue;
            }
            if (!identity.VerifyKeys.Keys.TryGetValue(new KeyIdentifier(algorithm, keyName), out var key))
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
                supportedSignatures.Add((algorithm, key, signatureBytes));
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
            if (VerifySignature(algorithm, key, jsonBytes, signature))
            {
                return true;
            }
        }
        return false;
    }
}
