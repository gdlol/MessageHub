using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer;

[JsonConverter(typeof(RoomStateKeyConverter))]
public record RoomStateKey(string EventType, string StateKey);

public class RoomStateKeyConverter : JsonConverter<RoomStateKey>
{
    private static string ToString(RoomStateKey roomStateKey)
    {
        return JsonSerializer.Serialize(new[] { roomStateKey.EventType, roomStateKey.StateKey });
    }

    private static RoomStateKey Parse(string s)
    {
        var fields = JsonSerializer.Deserialize<string[]>(s);
        if (fields is null)
        {
            throw new InvalidOperationException();
        }
        return new RoomStateKey(fields[0], fields[1]);
    }

    public override RoomStateKey? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        if (s is null)
        {
            return null;
        }
        return Parse(s);
    }

    public override void Write(Utf8JsonWriter writer, RoomStateKey value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToString(value));
    }

    public override RoomStateKey ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        return Parse(s!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, RoomStateKey value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(ToString(value));
    }
}
