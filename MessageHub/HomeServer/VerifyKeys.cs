using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer;

public record class ParseKeyIdentifierOperation(string String);

[JsonConverter(typeof(KeyIdentifierConverter))]
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

    public static KeyIdentifier Parse(string s)
    {
        if (!TryParse(s, out var keyIdentifier))
        {
            throw new InvalidOperationException(nameof(Parse));
        }
        return keyIdentifier;
    }
}

public class KeyIdentifierConverter : JsonConverter<KeyIdentifier>
{
    public override KeyIdentifier? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        if (s is null)
        {
            return null;
        }
        if (KeyIdentifier.TryParse(s, out var identifier))
        {
            return identifier;
        }
        else
        {
            var operation = new ParseKeyIdentifierOperation(s);
            throw new InvalidOperationException(operation.ToString());
        }
    }

    public override void Write(Utf8JsonWriter writer, KeyIdentifier value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    public override KeyIdentifier ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        return KeyIdentifier.Parse(s!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, KeyIdentifier value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
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
