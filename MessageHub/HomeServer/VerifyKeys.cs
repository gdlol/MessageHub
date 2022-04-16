using System.Collections.Immutable;

namespace MessageHub.HomeServer;

public record class KeyIdentifier(string Algorithm, string KeyName)
{
    public override string ToString()
    {
        return $"{Algorithm}:{KeyName}";
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
