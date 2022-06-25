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
    private readonly EphemeralLoader ephemeralLoader;

    public RoomsLoader(
        ITimelineLoader timelineLoader,
        IRooms rooms,
        AccountDataLoader accountDataLoader,
        EphemeralLoader ephemeralLoader)
    {
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(accountDataLoader);
        ArgumentNullException.ThrowIfNull(ephemeralLoader);

        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
        this.accountDataLoader = accountDataLoader;
        this.ephemeralLoader = ephemeralLoader;
    }

    private static bool ShouldGetTimeline(string roomId, RoomEventFilter? filter)
    {
        return filter.ShouldIncludeRoomId(roomId);
    }

    private static bool ShouldGetStateUpdate(string roomId, StateFilter? filter)
    {
        return filter.ShouldIncludeRoomId(roomId);
    }

    internal static Func<PersistentDataUnit, bool> GetTimelineEventFilter(RoomEventFilter? filter)
    {
        return pdu => filter.ShouldIncludeEvent(pdu.Sender, pdu.EventType, pdu.Content);
    }

    private static IEnumerable<PersistentDataUnit> FilterStateEvents(
        IEnumerable<PersistentDataUnit> stateEvents,
        StateFilter? filter)
    {
        return stateEvents
            .Where(pdu => filter.ShouldIncludeEvent(pdu.Sender, pdu.EventType, pdu.Content))
            .ApplyLimit(filter);
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
        string since,
        RoomFilter? filter)
    {
        var batchStates = await timelineLoader.LoadBatchStatesAsync(filter.ShouldIncludeRoomId, true);
        var sinceEventIds = await timelineLoader.GetRoomEventIds(since);

        bool includeLeave = filter?.IncludeLeave == true;
        var membershipUpdateRoomIds = new HashSet<string>();
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            if (batchStates.RoomEventIds.TryGetValue(roomId, out string? currentId)
                && sinceEventIds.TryGetValue(roomId, out string? sinceId)
                && currentId != sinceId)
            {
                // Recently joined room.
                membershipUpdateRoomIds.Add(roomId);
            }
        }
        foreach (string roomId in batchStates.LeftRoomIds)
        {
            if (batchStates.RoomEventIds.TryGetValue(roomId, out string? currentId)
                && sinceEventIds.TryGetValue(roomId, out string? sinceId)
                && currentId != sinceId)
            {
                // Recently left room.
                includeLeave = true;
                membershipUpdateRoomIds.Add(roomId);
            }
        }
        var rooms = new Rooms
        {
            Invite = new Dictionary<string, InvitedRoom>(),
            Join = new Dictionary<string, JoinedRoom>(),
            Knock = new Dictionary<string, KnockedRoom>(),
            Leave = includeLeave ? new Dictionary<string, LeftRoom>() : null
        };
        if (since == string.Empty && timelineLoader.IsEmpty)
        {
            return (string.Empty, rooms);
        }

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
                AccountData = await accountDataLoader.LoadAccountDataAsync(userId, roomId, filter?.AccountData),
                Ephemeral = await ephemeralLoader.LoadEphemeralEventsAsync(roomId, filter?.Ephemeral)
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
            using var _ = iterator;
            using var roomEventStore = await this.rooms.GetRoomEventStoreAsync(roomId);

            var timelineEvents = new List<PersistentDataUnit>();
            bool? limited = null;
            string? previousEventId = null;
            PersistentDataUnit[] previousStateEvents;
            if (ShouldGetTimeline(roomId, filter?.Timeline))
            {
                while (true)
                {
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
                    if (iterator.CurrentEventId == sinceEventId)
                    {
                        previousEventId = sinceEventId;
                        var stateEvents = await roomEventStore.LoadStateEventsAsync(iterator.CurrentEventId);
                        previousStateEvents = stateEvents.Values.ToArray();
                        break;
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
                if (fullState || membershipUpdateRoomIds.Contains(roomId) || sinceEventId is null)
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
            var (timeline, stateUpdate) = await LoadRecentEvents(roomId);
            room.Timeline = timeline;
            room.State = stateUpdate;
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
