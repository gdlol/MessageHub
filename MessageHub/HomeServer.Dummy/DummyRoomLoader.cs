using MessageHub.ClientServerProtocol;

namespace MessageHub.HomeServer.Dummy;

public class DummyRoomLoader : IRoomLoader
{
    public bool IsEmpty => RoomHistory.RoomStates.IsEmpty;

    public Task<IRoomStates> LoadRoomStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        if (RoomHistory.RoomStates.IsEmpty)
        {
            throw new InvalidOperationException();
        }
        var roomState = RoomHistory.RoomStates[^1];
        roomState = roomState.Filter(roomIdFilter, includeLeave);
        return Task.FromResult<IRoomStates>(roomState);
    }

    public Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? since)
    {
        var result = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(since))
        {
            var roomStates = RoomHistory.TryGetRoomStates(since);
            if (roomStates is not null)
            {
                foreach (var (roomId, room) in roomStates.Rooms)
                {
                    result[roomId] = room.EventIds[^1];
                }
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }

    public ValueTask<ClientEventWithoutRoomID[]> GetRoomStateEvents(string roomId, string? sinceEventId)
    {
        if (RoomHistory.RoomStates.IsEmpty)
        {
            throw new InvalidOperationException();
        }
        var roomStates = RoomHistory.RoomStates[^1];
        var room = roomStates.Rooms[roomId];
        var result = new List<ClientEventWithoutRoomID>();
        if (sinceEventId is not null)
        {
            foreach (var (_, eventId) in room.States[sinceEventId])
            {
                var clientEvent = room.LoadClientEvent(eventId);
                result.Add(clientEvent);
            }
        }
        return ValueTask.FromResult(result.ToArray());
    }
}
