using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;

namespace MessageHub.ClientServerApi.Sync;

public class RoomLoader
{
    private readonly IRoomLoader roomLoader;
    private readonly AccountDataLoader accountDataLoader;

    public RoomLoader(IRoomLoader roomLoader, AccountDataLoader accountDataLoader)
    {
        ArgumentNullException.ThrowIfNull(roomLoader);
        ArgumentNullException.ThrowIfNull(accountDataLoader);

        this.roomLoader = roomLoader;
        this.accountDataLoader = accountDataLoader;
    }

    private static Func<string, bool> GetRoomIdFilter(string[]? rooms, string[]? notRooms)
    {
        return roomId =>
        {
            if (rooms is not null && !rooms.Contains(roomId))
            {
                return false;
            }
            if (notRooms is not null && notRooms.Contains(roomId))
            {
                return false;
            }
            return true;
        };
    }

    private static bool ShouldGetTimeline(string roomId, RoomEventFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.Rooms is not null && !filter.Rooms.Contains(roomId))
        {
            return false;
        }
        if (filter.NotRooms is not null && filter.NotRooms.Contains(roomId))
        {
            return false;
        }
        return true;
    }

    private static bool ShouldGetStateUpdate(string roomId, StateFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }
        if (filter.Rooms is not null && !filter.Rooms.Contains(roomId))
        {
            return false;
        }
        if (filter.NotRooms is not null && filter.NotRooms.Contains(roomId))
        {
            return false;
        }
        return true;
    }

    private static Func<ClientEventWithoutRoomID, bool> GetTimelineEventFilter(RoomEventFilter? filter)
    {
        if (filter is null)
        {
            return _ => true;
        }
        return clientEvent =>
        {
            if (filter.ContainsUrl is not null
                && clientEvent.Content.TryGetProperty("url", out var _) != filter.ContainsUrl.Value)
            {
                return false;
            }
            if (filter.Senders is not null && !filter.Senders.Contains(clientEvent.Sender))
            {
                return false;
            }
            if (filter.NotSenders is not null && filter.NotSenders.Contains(clientEvent.Sender))
            {
                return false;
            }
            if (filter.Types is not null
                && !filter.Types.Any(pattern => Filter.StringMatch(clientEvent.EventType, pattern)))
            {
                return false;
            }
            if (filter.NotTypes is not null
                && filter.NotTypes.Any(pattern => Filter.StringMatch(clientEvent.EventType, pattern)))
            {
                return false;
            }
            return true;
        };
    }

    private static ClientEventWithoutRoomID[] FilterStateEvents(
        IEnumerable<ClientEventWithoutRoomID> stateEvents,
        StateFilter? filter)
    {
        if (filter is null)
        {
            return stateEvents.ToArray();
        }
        var result = new List<ClientEventWithoutRoomID>();
        foreach (var clientEvent in stateEvents)
        {
            if (filter.Limit is not null && filter.Limit >= result.Count)
            {
                break;
            }
            if (filter.ContainsUrl is not null
                && clientEvent.Content.TryGetProperty("url", out var _) != filter.ContainsUrl.Value)
            {
                continue;
            }
            if (filter.Senders is not null && !filter.Senders.Contains(clientEvent.Sender))
            {
                continue;
            }
            if (filter.NotSenders is not null && filter.NotSenders.Contains(clientEvent.Sender))
            {
                continue;
            }
            if (filter.Types is not null
                && !filter.Types.Any(pattern => Filter.StringMatch(clientEvent.EventType, pattern)))
            {
                continue;
            }
            if (filter.NotTypes is not null
                && filter.NotTypes.Any(pattern => Filter.StringMatch(clientEvent.EventType, pattern)))
            {
                continue;
            }
            result.Add(clientEvent);
        }
        return result.ToArray();
    }

    private static ClientEventWithoutRoomID[] ComputeStateDelta(
        IEnumerable<ClientEventWithoutRoomID> sinceStateEvents,
        IEnumerable<ClientEventWithoutRoomID> previousStateEvents)
    {
        var sinceState = sinceStateEvents.ToDictionary(x => new RoomStateKey(x.EventType, x.StateKey!), x => x);
        var previousState = previousStateEvents.ToDictionary(x => new RoomStateKey(x.EventType, x.StateKey!), x => x);
        var delta = new List<ClientEventWithoutRoomID>();
        foreach (var (key, event1) in previousState)
        {
            if (sinceState.TryGetValue(key, out var event2) && event1.EventId == event2.EventId)
            {
                continue;
            }
            delta.Add(event1);
        }
        return delta.ToArray();
    }

    public async Task<(string nextBatch, Rooms rooms)> LoadRoomsAsync(
        string userId,
        bool fullState,
        string? since,
        RoomFilter? filter)
    {
        bool includeLeave = filter?.IncludeLeave == true;
        var rooms = new Rooms
        {
            Invite = new Dictionary<string, InvitedRoom>(),
            Join = new Dictionary<string, JoinedRoom>(),
            Knock = new Dictionary<string, KnockedRoom>(),
            Leave = includeLeave ? new Dictionary<string, LeftRoom>() : null
        };
        if (string.IsNullOrEmpty(since) && roomLoader.IsEmpty)
        {
            return (string.Empty, rooms);
        }

        var roomIdFilter = GetRoomIdFilter(filter?.Rooms, filter?.NotRooms);
        var roomStates = await roomLoader.LoadRoomStatesAsync(roomIdFilter, includeLeave);
        foreach (string roomId in roomStates.InvitedRoomIds)
        {
            rooms.Invite[roomId] = new InvitedRoom
            {
                InviteState = new InviteState
                {
                    Events = roomStates.GetStrippedStateEvents(roomId)
                }
            };
        }
        foreach (string roomId in roomStates.JoinedRoomIds)
        {
            rooms.Join[roomId] = new JoinedRoom
            {
                AccountData = await accountDataLoader.LoadAccountDataAsync(userId, roomId, filter?.AccountData)
            };
        }
        foreach (string roomId in roomStates.KnockedRoomIds)
        {
            rooms.Knock[roomId] = new KnockedRoom
            {
                KnockState = new KnockState
                {
                    Events = roomStates.GetStrippedStateEvents(roomId)
                }
            };
        }
        if (rooms.Leave is not null)
        {
            foreach (string roomId in roomStates.LeftRoomIds)
            {
                rooms.Leave[roomId] = new LeftRoom
                {
                    AccountData = await accountDataLoader.LoadAccountDataAsync(userId, roomId, filter?.AccountData)
                };
            }
        }

        if (since == roomStates.BatchId)
        {
            return (since, rooms);
        }

        var roomEventIds = await roomLoader.GetRoomEventIds(since);
        var timelineEventFilter = GetTimelineEventFilter(filter?.Timeline);
        async Task<(Timeline? timeline, State? stateUpdate)> LoadRecentEvents(string roomId)
        {
            roomEventIds.TryGetValue(roomId, out string? sinceEventId);
            var iterator = await roomStates.GetTimelineIteratorAsync(roomId);
            var timelineEvents = new List<ClientEventWithoutRoomID>();
            bool? limited = null;
            string? previousEventId = null;
            ClientEventWithoutRoomID[] previousStateEvents;
            if (ShouldGetTimeline(roomId, filter?.Timeline))
            {
                while (true)
                {
                    if (iterator.CurrentEvent.EventId == sinceEventId)
                    {
                        previousEventId = sinceEventId;
                        previousStateEvents = iterator.GetStateEvents();
                        break;
                    }
                    if (filter?.Timeline?.Limit is int limit && timelineEvents.Count >= limit)
                    {
                        limited = true;
                        previousEventId = iterator.CurrentEvent.EventId;
                        previousStateEvents = iterator.GetStateEvents();
                        break;
                    }
                    if (timelineEventFilter(iterator.CurrentEvent))
                    {
                        timelineEvents.Add(iterator.CurrentEvent);
                    }
                    if (!await iterator.TryMoveBackwardAsync())
                    {
                        previousStateEvents = Array.Empty<ClientEventWithoutRoomID>();
                        break;
                    }
                }
                timelineEvents.Reverse();
            }
            else
            {
                previousEventId = iterator.CurrentEvent.EventId;
                previousStateEvents = iterator.GetStateEvents();
            }
            var timeline = new Timeline
            {
                Events = timelineEvents.ToArray(),
                Limited = limited,
                PreviousBatch = previousEventId
            };
            State? stateUpdate = null;
            if (ShouldGetStateUpdate(roomId, filter?.State))
            {
                if (fullState)
                {
                    stateUpdate = new State
                    {
                        Events = FilterStateEvents(previousStateEvents, filter?.State)
                    };
                }
                else
                {
                    var sinceStateEvents = await roomLoader.GetRoomStateEvents(roomId, sinceEventId);
                    var delta = ComputeStateDelta(sinceStateEvents, previousStateEvents);
                    stateUpdate = new State
                    {
                        Events = FilterStateEvents(delta, filter?.State)
                    };
                }
            }
            return (timeline, stateUpdate);
        }
        foreach (string roomId in roomStates.JoinedRoomIds)
        {
            var room = rooms.Join[roomId];
            var (timeline, previousState) = await LoadRecentEvents(roomId);
            room.Timeline = timeline;
            room.State = previousState;
        }
        if (rooms.Leave is not null)
        {
            foreach (string roomId in roomStates.LeftRoomIds)
            {
                var room = rooms.Leave[roomId];
                var (timeline, stateUpdate) = await LoadRecentEvents(roomId);
                room.Timeline = timeline;
                room.State = stateUpdate;
            }
        }
        return (roomStates.BatchId, rooms);
    }
}
