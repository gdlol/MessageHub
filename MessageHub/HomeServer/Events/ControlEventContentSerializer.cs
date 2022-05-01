using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MessageHub.HomeServer.Events.Room;

namespace MessageHub.HomeServer.Events;

public class ControlEventContentSerializer
{
    public static ImmutableDictionary<string, Type> ControlEventTypes { get; } = new Dictionary<string, Type>
    {
        [EventTypes.Create] = typeof(CreateEvent),
        [EventTypes.Member] = typeof(MemberEvent),
        [EventTypes.PowerLevels] = typeof(PowerLevelsEvent),
        [EventTypes.JoinRules] = typeof(JoinRulesEvent)
    }.ToImmutableDictionary();

    public static bool TryDeserialize(
        string eventType,
        JsonElement content,
        [NotNullWhen(true)] out object? controlEventContent)
    {
        controlEventContent = null;
        if (ControlEventTypes.TryGetValue(eventType, out var contentType))
        {
            controlEventContent = JsonSerializer.Deserialize(content, contentType);
            if (controlEventContent is null)
            {
                throw new InvalidOperationException();
            }
            return true;
        }
        return false;
    }
}
