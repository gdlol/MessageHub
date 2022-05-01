using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.ClientServer.Sync;

public class RoomsLoader
{
    private readonly ITimelineLoader timelineLoader;
    private readonly AccountDataLoader accountDataLoader;

    public RoomsLoader(ITimelineLoader timelineLoader, AccountDataLoader accountDataLoader)
    {
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(accountDataLoader);

        this.timelineLoader = timelineLoader;
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

    internal static Func<PersistentDataUnit, bool> GetTimelineEventFilter(RoomEventFilter? filter)
    {
        if (filter is null)
        {
            return _ => true;
        }
        return pdu =>
        {
            if (filter.ContainsUrl is not null
                && pdu.Content.TryGetProperty("url", out var _) != filter.ContainsUrl.Value)
            {
                return false;
            }
            if (filter.Senders is not null && !filter.Senders.Contains(pdu.Sender))
            {
                return false;
            }
            if (filter.NotSenders is not null && filter.NotSenders.Contains(pdu.Sender))
            {
                return false;
            }
            if (filter.Types is not null
                && !filter.Types.Any(pattern => Filter.StringMatch(pdu.EventType, pattern)))
            {
                return false;
            }
            if (filter.NotTypes is not null
                && filter.NotTypes.Any(pattern => Filter.StringMatch(pdu.EventType, pattern)))
            {
                return false;
            }
            return true;
        };
    }

    private static PersistentDataUnit[] FilterStateEvents(
        IEnumerable<PersistentDataUnit> stateEvents,
        StateFilter? filter)
    {
        if (filter is null)
        {
            return stateEvents.ToArray();
        }
        var result = new List<PersistentDataUnit>();
        foreach (var pdu in stateEvents)
        {
            if (filter.Limit is not null && filter.Limit >= result.Count)
            {
                break;
            }
            if (filter.ContainsUrl is not null
                && pdu.Content.TryGetProperty("url", out var _) != filter.ContainsUrl.Value)
            {
                continue;
            }
            if (filter.Senders is not null && !filter.Senders.Contains(pdu.Sender))
            {
                continue;
            }
            if (filter.NotSenders is not null && filter.NotSenders.Contains(pdu.Sender))
            {
                continue;
            }
            if (filter.Types is not null
                && !filter.Types.Any(pattern => Filter.StringMatch(pdu.EventType, pattern)))
            {
                continue;
            }
            if (filter.NotTypes is not null
                && filter.NotTypes.Any(pattern => Filter.StringMatch(pdu.EventType, pattern)))
            {
                continue;
            }
            result.Add(pdu);
        }
        return result.ToArray();
    }

    private static PersistentDataUnit[] ComputeStateDelta(
        IEnumerable<PersistentDataUnit> sinceStateEvents,
        IEnumerable<PersistentDataUnit> previousStateEvents)
    {
        var sinceState = sinceStateEvents.ToDictionary(x => new RoomStateKey(x.EventType, x.StateKey!), x => x);
        var previousState = previousStateEvents.ToDictionary(x => new RoomStateKey(x.EventType, x.StateKey!), x => x);
        var delta = new List<PersistentDataUnit>();
        foreach (var (key, event1) in previousState)
        {
            if (sinceState.TryGetValue(key, out var event2)
                && EventHash.GetEventId(event1) == EventHash.GetEventId(event2))
            {
                continue;
            }
            delta.Add(event1);
        }
        return delta.ToArray();
    }

    public string CurrentBatchId => timelineLoader.CurrentBatchId;

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
        if (string.IsNullOrEmpty(since) && timelineLoader.IsEmpty)
        {
            return (string.Empty, rooms);
        }

        var roomIdFilter = GetRoomIdFilter(filter?.Rooms, filter?.NotRooms);
        var roomStates = await timelineLoader.LoadRoomStatesAsync(roomIdFilter, includeLeave);
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

        var sinceEventIds = await timelineLoader.GetRoomEventIds(since);
        var currentEventIds = await timelineLoader.GetRoomEventIds(roomStates.BatchId);
        var timelineEventFilter = GetTimelineEventFilter(filter?.Timeline);
        async Task<(Timeline? timeline, State? stateUpdate)> LoadRecentEvents(string roomId)
        {
            sinceEventIds.TryGetValue(roomId, out string? sinceEventId);
            string currentEventId = currentEventIds[roomId];
            var iterator = await timelineLoader.GetTimelineIteratorAsync(roomId, currentEventId);
            if (iterator is null)
            {
                throw new InvalidOperationException();
            }
            var timelineEvents = new List<PersistentDataUnit>();
            bool? limited = null;
            string? previousEventId = null;
            PersistentDataUnit[] previousStateEvents;
            if (ShouldGetTimeline(roomId, filter?.Timeline))
            {
                while (true)
                {
                    string eventId = EventHash.GetEventId(iterator.CurrentEvent);
                    if (eventId == sinceEventId)
                    {
                        previousEventId = sinceEventId;
                        previousStateEvents = iterator.GetStateEvents();
                        break;
                    }
                    if (filter?.Timeline?.Limit is int limit && timelineEvents.Count >= limit)
                    {
                        limited = true;
                        previousEventId = eventId;
                        previousStateEvents = iterator.GetStateEvents();
                        break;
                    }
                    if (timelineEventFilter(iterator.CurrentEvent))
                    {
                        timelineEvents.Add(iterator.CurrentEvent);
                    }
                    if (!await iterator.TryMoveBackwardAsync())
                    {
                        previousStateEvents = Array.Empty<PersistentDataUnit>();
                        break;
                    }
                }
                timelineEvents.Reverse();
            }
            else
            {
                previousEventId = EventHash.GetEventId(iterator.CurrentEvent);
                previousStateEvents = iterator.GetStateEvents();
            }
            var timeline = new Timeline
            {
                Events = timelineEvents.Select(ClientEventWithoutRoomID.FromPersistentDataUnit).ToArray(),
                Limited = limited,
                PreviousBatch = previousEventId
            };
            State? stateUpdate = null;
            if (ShouldGetStateUpdate(roomId, filter?.State))
            {
                if (fullState || sinceEventId is null)
                {
                    stateUpdate = new State
                    {
                        Events = FilterStateEvents(previousStateEvents, filter?.State)
                            .Select(ClientEventWithoutRoomID.FromPersistentDataUnit)
                            .ToArray()
                    };
                }
                else
                {
                    iterator = await timelineLoader.GetTimelineIteratorAsync(roomId, sinceEventId);
                    if (iterator is null)
                    {
                        throw new InvalidOperationException();
                    }
                    var sinceStateEvents = iterator!.GetStateEvents();
                    var delta = ComputeStateDelta(sinceStateEvents, previousStateEvents);
                    stateUpdate = new State
                    {
                        Events = FilterStateEvents(delta, filter?.State)
                            .Select(ClientEventWithoutRoomID.FromPersistentDataUnit)
                            .ToArray()
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
