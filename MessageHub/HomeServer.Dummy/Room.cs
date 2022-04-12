using System.Collections.Immutable;
using System.Text.Json;

namespace MessageHub.HomeServer.Dummy;

using RoomState = ImmutableDictionary<RoomStateKey, string>;

public class Room
{
    public ImmutableList<string> EventIds { get; }

    public ImmutableDictionary<string, string> Events { get; }

    public ImmutableDictionary<string, RoomState> States { get; }

    private Room(
        ImmutableList<string> eventIds,
        ImmutableDictionary<string, string> events,
        ImmutableDictionary<string, RoomState> states)
    {
        EventIds = eventIds;
        Events = events;
        States = states;
    }

    public static Room Empty { get; } = new Room(
        ImmutableList<string>.Empty,
        ImmutableDictionary<string, string>.Empty,
        ImmutableDictionary<string, RoomState>.Empty);

    public (string, PersistentDataUnit)[] GetTimeline(string? since, int limit)
    {
        var result = new List<(string, PersistentDataUnit)>();
        foreach (var eventId in EventIds.Reverse())
        {
            if (result.Count >= limit || eventId == since)
            {
                break;
            }
            string json = Events[eventId];
            var pdu = JsonSerializer.Deserialize<PersistentDataUnit>(json)!;
            result.Add((eventId, pdu));
        }
        return result.ToArray();
    }

    public RoomState GetPreviousState(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        string json = Events[eventId];
        var pdu = JsonSerializer.Deserialize<PersistentDataUnit>(json)!;
        return pdu.PreviousEvents.Length == 0 ? RoomState.Empty : States[pdu.PreviousEvents[0]];
    }

    public Room AddEvent(PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        string eventId = pdu.GetEventId();
        RoomState? roomState;
        if (Events.Count == 0)
        {
            roomState = RoomState.Empty;
        }
        else
        {
            string previousEventId = pdu.PreviousEvents[0];
            roomState = States[previousEventId];
        }
        if (pdu.StateKey is not null)
        {
            roomState = roomState.SetItem(new RoomStateKey(pdu.EventType, pdu.StateKey), eventId);
        }
        var eventIds = EventIds.Add(eventId);
        var events = Events.Add(eventId, pdu.ToCanonicalJson());
        var states = States.Add(eventId, roomState);
        return new Room(eventIds, events, states);
    }
}
