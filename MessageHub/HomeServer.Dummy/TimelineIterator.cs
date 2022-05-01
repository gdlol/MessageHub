using MessageHub.ClientServer.Protocol;

namespace MessageHub.HomeServer.Dummy;

internal class TimelineIterator
{
    private readonly Room room;

    public ClientEventWithoutRoomID CurrentEvent { get; private set; }

    private int index;

    public TimelineIterator(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        this.room = room;
        index = room.EventIds.Count - 1;
        CurrentEvent = room.LoadClientEventWithoutRoomId(room.EventIds[index]);
    }

    public TimelineIterator(Room room, string eventId)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(eventId);

        this.room = room;
        index = room.EventIds.IndexOf(eventId);
        CurrentEvent = room.LoadClientEventWithoutRoomId(room.EventIds[index]);
    }

    public ValueTask<bool> TryMoveForwardAsync()
    {
        if (index >= room.EventIds.Count - 1)
        {
            return ValueTask.FromResult(false);
        }
        index += 1;
        CurrentEvent = room.LoadClientEventWithoutRoomId(room.EventIds[index]);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> TryMoveBackwardAsync()
    {
        if (index <= 0)
        {
            return ValueTask.FromResult(false);
        }
        index -= 1;
        CurrentEvent = room.LoadClientEventWithoutRoomId(room.EventIds[index]);
        return ValueTask.FromResult(true);
    }

    public ClientEventWithoutRoomID[] GetStateEvents()
    {
        var result = new List<ClientEventWithoutRoomID>();
        var states = room.States[CurrentEvent.EventId];
        foreach (var (_, eventId) in states)
        {
            var clientEvent = room.LoadClientEventWithoutRoomId(eventId);
            result.Add(clientEvent);
        }
        return result.ToArray();
    }
}
