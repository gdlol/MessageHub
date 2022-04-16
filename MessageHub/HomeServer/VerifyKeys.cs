using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace MessageHub.HomeServer;

public record class KeyIdentifier(string Algorithm, string KeyName)
{
    public override string ToString()
    {
        return $"{Algorithm}:{KeyName}";
    }

    public static bool TryParse(string? s, [NotNullWhen(true)] out KeyIdentifier? identifier)
    {
        identifier = null;
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        var parts = s.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }
        identifier = new KeyIdentifier(parts[0], parts[1]);
        return true;
    }
}

public class VerifyKeys
{
    public ImmutableDictionary<KeyIdentifier, string> Keys { get; }
    public long ExpireTimestamp { get; }

    public VerifyKeys(ImmutableDictionary<KeyIdentifier, string> keys, long expireTimestamp)
    {
        ArgumentNullException.ThrowIfNull(keys);

        Keys = keys;
        ExpireTimestamp = expireTimestamp;
    }
}
