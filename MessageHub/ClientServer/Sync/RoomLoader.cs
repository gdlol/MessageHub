using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.ClientServer.Sync;

public class RoomsLoader
{
    private readonly ITimelineLoader timelineLoader;
    private readonly IRooms rooms;
    private readonly AccountDataLoader accountDataLoader;

    public RoomsLoader(ITimelineLoader timelineLoader, IRooms rooms, AccountDataLoader accountDataLoader)
    {
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(accountDataLoader);

        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
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
        var batchStates = await timelineLoader.LoadBatchStatesAsync(roomIdFilter, includeLeave);
        foreach (var (roomId, stateEvents) in batchStates.Invites)
        {
            rooms.Invite[roomId] = new InvitedRoom
            {
                InviteState = new InviteState
                {
                    Events = stateEvents.ToArray()
                }
            };
        }
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            rooms.Join[roomId] = new JoinedRoom
            {
                AccountData = await accountDataLoader.LoadAccountDataAsync(userId, roomId, filter?.AccountData)
            };
        }
        foreach (var (roomId, stateEvents) in batchStates.Knocks)
        {
            rooms.Knock[roomId] = new KnockedRoom
            {
                KnockState = new KnockState
                {
                    Events = stateEvents.ToArray()
                }
            };
        }
        if (rooms.Leave is not null)
        {
            foreach (string roomId in batchStates.LeftRoomIds)
            {
                rooms.Leave[roomId] = new LeftRoom
                {
                    AccountData = await accountDataLoader.LoadAccountDataAsync(userId, roomId, filter?.AccountData)
                };
            }
        }

        if (since == batchStates.BatchId)
        {
            return (since, rooms);
        }

        var sinceEventIds = await timelineLoader.GetRoomEventIds(since);
        var currentEventIds = await timelineLoader.GetRoomEventIds(batchStates.BatchId);
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
            using var roomEventStore = await this.rooms.GetRoomEventStoreAsync(roomId);

            var timelineEvents = new List<PersistentDataUnit>();
            bool? limited = null;
            string? previousEventId = null;
            PersistentDataUnit[] previousStateEvents;
            if (ShouldGetTimeline(roomId, filter?.Timeline))
            {
                while (true)
                {
                    if (iterator.CurrentEventId == sinceEventId)
                    {
                        previousEventId = sinceEventId;
                        var stateEvents = await roomEventStore.LoadStateEventsAsync(iterator.CurrentEventId);
                        previousStateEvents = stateEvents.Values.ToArray();
                        break;
                    }
                    if (filter?.Timeline?.Limit is int limit && timelineEvents.Count >= limit)
                    {
                        limited = true;
                        previousEventId = iterator.CurrentEventId;
                        var stateEvents = await roomEventStore.LoadStateEventsAsync(iterator.CurrentEventId);
                        previousStateEvents = stateEvents.Values.ToArray();
                        break;
                    }
                    var currentEvent = await roomEventStore.LoadEventAsync(iterator.CurrentEventId);
                    if (timelineEventFilter(currentEvent))
                    {
                        timelineEvents.Add(currentEvent);
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
                previousEventId = iterator.CurrentEventId;
                var stateEvents = await roomEventStore.LoadStateEventsAsync(iterator.CurrentEventId);
                previousStateEvents = stateEvents.Values.ToArray();
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
                    var sinceStateEvents = await roomEventStore.LoadStateEventsAsync(sinceEventId);
                    var delta = ComputeStateDelta(sinceStateEvents.Values, previousStateEvents);
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
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            var room = rooms.Join[roomId];
            var (timeline, previousState) = await LoadRecentEvents(roomId);
            room.Timeline = timeline;
            room.State = previousState;
        }
        if (rooms.Leave is not null)
        {
            foreach (string roomId in batchStates.LeftRoomIds)
            {
                var room = rooms.Leave[roomId];
                var (timeline, stateUpdate) = await LoadRecentEvents(roomId);
                room.Timeline = timeline;
                room.State = stateUpdate;
            }
        }
        return (batchStates.BatchId, rooms);
    }
}
