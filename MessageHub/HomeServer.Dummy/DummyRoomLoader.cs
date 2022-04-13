using System.Text.Json;
using MessageHub.ClientServerProtocol;
using M = MessageHub.ClientServerProtocol.Events;

namespace MessageHub.HomeServer.Dummy;

public class DummyRoomLoader : IRoomLoader
{
    public bool IsEmpty => RoomHistory.RoomStates.IsEmpty;

    public string CurrentBatchId => RoomHistory.RoomStates.IsEmpty ? string.Empty : RoomHistory.RoomStates[^1].BatchId;

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

    public bool HasRoom(string roomId)
    {
        if (RoomHistory.RoomStates.IsEmpty)
        {
            return false;
        }
        return RoomHistory.RoomStates[^1].Rooms.ContainsKey(roomId);
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
                var clientEvent = room.LoadClientEventWithoutRoomId(eventId);
                result.Add(clientEvent);
            }
        }
        return ValueTask.FromResult(result.ToArray());
    }

    public Task<ClientEvent?> LoadEventAsync(string roomId, string eventId)
    {
        ClientEvent? result = null;
        if (!RoomHistory.RoomStates.IsEmpty)
        {
            var roomStates = RoomHistory.RoomStates[^1];
            if (roomStates.Rooms.TryGetValue(roomId, out var room)
                && room.Events.ContainsKey(eventId))
            {
                result = room.LoadClientEvent(eventId);
            }
        }
        return Task.FromResult(result);
    }

    public Task<ClientEvent[]?> LoadRoomMembersAsync(string roomId, string? sinceEventId)
    {
        ClientEvent[]? result = null;
        if (!RoomHistory.RoomStates.IsEmpty)
        {
            var roomStates = RoomHistory.RoomStates[^1];
            if (roomStates.Rooms.TryGetValue(roomId, out var room))
            {
                var roomState = room.States[sinceEventId ?? room.EventIds[^1]];
                var memberEvents = new List<ClientEvent>();
                foreach (var (stateKey, eventId) in roomState)
                {
                    if (stateKey.EventType == M.Room.EventTypes.Member)
                    {
                        var clientEvent = room.LoadClientEvent(eventId);
                        memberEvents.Add(clientEvent);
                    }
                }
                result = memberEvents.ToArray();
            }
        }
        return Task.FromResult(result);
    }

    public Task<JsonElement?> LoadStateAsync(string roomId, RoomStateKey stateKey)
    {
        JsonElement? result = null;
        if (!RoomHistory.RoomStates.IsEmpty)
        {
            var roomStates = RoomHistory.RoomStates[^1];
            if (roomStates.Rooms.TryGetValue(roomId, out var room))
            {
                var roomState = room.States[room.EventIds[^1]];
                if (roomState.TryGetValue(stateKey, out string? eventId))
                {
                    var clientEvent = room.LoadClientEvent(eventId);
                    result = clientEvent.Content;
                }
            }
        }
        return Task.FromResult(result);
    }

    public Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId)
    {
        ITimelineIterator? result = null;
        if (!RoomHistory.RoomStates.IsEmpty)
        {
            var roomStates = RoomHistory.RoomStates[^1];
            if (roomStates.Rooms.TryGetValue(roomId, out var room))
            {
                if (room.EventIds.Contains(eventId))
                {
                    ITimelineIterator iterator = new TimelineIterator(room, eventId);
                }
            }
        }
        return Task.FromResult(result);
    }
}
