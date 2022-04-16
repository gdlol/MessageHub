using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer.Dummy;

using RoomState = ImmutableDictionary<RoomStateKey, string>;

internal class Room
{
    public ImmutableList<string> EventIds { get; }

    public ImmutableDictionary<string, string> Events { get; }

    public ImmutableDictionary<string, RoomState> States { get; }

    public RoomMembership Membership { get; }

    private Room(
        ImmutableList<string> eventIds,
        ImmutableDictionary<string, string> events,
        ImmutableDictionary<string, RoomState> states,
        RoomMembership membership)
    {
        EventIds = eventIds;
        Events = events;
        States = states;
        Membership = membership;
    }

    private static Room Empty { get; } = new Room(
        ImmutableList<string>.Empty,
        ImmutableDictionary<string, string>.Empty,
        ImmutableDictionary<string, RoomState>.Empty,
        RoomMembership.Joined);

    public Room AddEvent(PersistentDataUnit pdu, RoomMembership newMembership)
    {
        ArgumentNullException.ThrowIfNull(pdu);

        string eventId = pdu.GetEventId();
        RoomState? roomState = Events.Count == 0 ? RoomState.Empty : States[EventIds[^1]];
        if (pdu.StateKey is not null)
        {
            roomState = roomState.SetItem(new RoomStateKey(pdu.EventType, pdu.StateKey), eventId);
        }
        var eventIds = EventIds.Add(eventId);
        var events = Events.Add(eventId, pdu.ToCanonicalJson());
        var states = States.Add(eventId, roomState);
        return new Room(eventIds, events, states, newMembership);
    }

    public static Room Create(PersistentDataUnit createEvent, RoomMembership membership)
    {
        return Empty.AddEvent(createEvent, membership);
    }

    public PersistentDataUnit LoadPdu(string eventId)
    {
        var pduJson = Events[eventId];
        var pdu = JsonSerializer.Deserialize<PersistentDataUnit>(pduJson)!;
        return pdu;
    }

    public ClientEventWithoutRoomID LoadClientEventWithoutRoomId(string eventId)
    {
        var pdu = LoadPdu(eventId);
        return new ClientEventWithoutRoomID
        {
            Content = pdu.Content,
            EventId = eventId,
            OriginServerTimestamp = pdu.OriginServerTimestamp,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey,
            EventType = pdu.EventType,
            Unsigned = pdu.Unsigned
        };
    }

    public ClientEvent LoadClientEvent(string eventId)
    {
        var pdu = LoadPdu(eventId);
        return new ClientEvent
        {
            Content = pdu.Content,
            EventId = eventId,
            OriginServerTimestamp = pdu.OriginServerTimestamp,
            RoomId = pdu.RoomId,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey,
            EventType = pdu.EventType,
            Unsigned = pdu.Unsigned
        };
    }
}
