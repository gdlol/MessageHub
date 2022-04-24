using System.Security.Cryptography;
using System.Text.Json;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer;

public static class EventHash
{
    public static byte[] ComputeHash(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        var element = pdu.ToJsonElement();
        var mapping = element.Deserialize<Dictionary<string, JsonElement>>()!;
        mapping.Remove("unsigned");
        mapping.Remove("signatures");
        mapping.Remove("hashes");
        var jsonBytes = CanonicalJson.SerializeToBytes(mapping);
        var hash = SHA256.HashData(jsonBytes);
        return hash;
    }

    public static void UpdateHash(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        var hash = ComputeHash(pdu);
        pdu.Hashes = new Dictionary<string, string>
        {
            ["sha256"] = UnpaddedBase64Encoder.Encode(hash)
        };
    }

    public static string GetEventId(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        var hash = pdu.Hashes.Values.SingleOrDefault();
        if (hash is null)
        {
            throw new InvalidOperationException();
        }
        return '$' + hash.Replace('+', '-').Replace('/', '_');
    }
}
