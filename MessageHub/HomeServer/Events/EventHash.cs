using System.Security.Cryptography;
using System.Text.Json;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer.Events;

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

    public static bool VerifyHash(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        if (pdu.Hashes.SingleOrDefault() is not (string algorithm, string hash)
            || algorithm != "sha256"
            || hash != UnpaddedBase64Encoder.Encode(ComputeHash(pdu)))
        {
            return false;
        }
        return true;
    }

    public static string? TryGetEventId(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        if (pdu.Hashes?.TryGetValue("sha256", out string? hash) != true || string.IsNullOrEmpty(hash))
        {
            return null;
        }
        return '$' + hash.Replace('+', '-').Replace('/', '_');
    }

    public static string GetEventId(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        string? eventId = TryGetEventId(pdu);
        if (eventId is null)
        {
            throw new InvalidOperationException();
        }
        return eventId;
    }
}
