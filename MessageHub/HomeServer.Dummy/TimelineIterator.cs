using System.Text.Json;
using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer.Dummy;

internal class TimelineIterator : ITimelineIterator
{
    private readonly Room room;

    public ClientEventWithoutRoomID CurrentEvent { get; private set; }

    private int index;

    public TimelineIterator(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        this.room = room;
        index = room.EventIds.Count - 1;
        CurrentEvent = room.LoadClientEvent(room.EventIds[index]);
    }

    public ValueTask<bool> TryMoveBackwardAsync()
    {
        if (index == 0)
        {
            return ValueTask.FromResult(false);
        }
        index -= 1;
        CurrentEvent = room.LoadClientEvent(room.EventIds[index]);
        return ValueTask.FromResult(true);
    }

    public ClientEventWithoutRoomID[] GetStateEvents()
    {
        var result = new List<ClientEventWithoutRoomID>();
        var states = room.States[CurrentEvent.EventId];
        foreach (var (_, eventId) in states)
        {
            var clientEvent = room.LoadClientEvent(eventId);
            result.Add(clientEvent);
        }
        return result.ToArray();
    }
}
