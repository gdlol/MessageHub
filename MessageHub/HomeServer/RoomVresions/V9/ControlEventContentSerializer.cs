using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer.RoomVersions.V9;

public class ControlEventContentSerializer
{
    public static ImmutableDictionary<string, Type> EventContentTypes { get; } = new Dictionary<string, Type>
    {
        [EventTypes.Create] = typeof(CreateEvent),
        [EventTypes.Member] = typeof(MemberEvent),
        [EventTypes.PowerLevels] = typeof(PowerLevelsEvent),
        [EventTypes.JoinRules] = typeof(JoinRulesEvent)
    }.ToImmutableDictionary();

    public static object TryDeserialize(string eventType, JsonElement content)
    {
        if (EventContentTypes.TryGetValue(eventType, out var contentType))
        {
            var result =  JsonSerializer.Deserialize(content, contentType);
            if (result is null)
            {
                throw new InvalidOperationException();
            }
            return result;
        }
        return content;
    }
}
