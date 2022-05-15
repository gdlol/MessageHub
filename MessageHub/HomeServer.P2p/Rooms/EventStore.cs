using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

using RoomCreators = ImmutableDictionary<string, string>;
using RoomEventIds = ImmutableDictionary<string, string>;
using States = ImmutableDictionary<RoomStateKey, string>;
using StrippedStates = ImmutableDictionary<string, ImmutableList<StrippedStateEvent>>;

internal record class TimelineRecord(string? PreviousEventId, string? NextEventId);

internal class EventStore
{
    public static EventStore? Instance { get; set; }

    public const string RoomCreatorsKey = "RoomCreators";
    public const string JoinedRoomIdsKey = "JoinedRoomIds";
    public const string LeftRoomIdsKey = "LeftRoomIds";
    public const string InvitesKey = "Invites";
    public const string KnocksKey = "Knocks";
    public const string CurrentBatchIdKey = "CurrentBatchId";

    public static string GetEventKey(string roomId, string eventId)
    {
        return $"Event:{JsonSerializer.Serialize(new[] { roomId, eventId })}";
    }

    public static string GetStatesKey(string roomId, string eventId)
    {
        return $"States:{JsonSerializer.Serialize(new[] { roomId, eventId })}";
    }

    public static string GetSnapshotKey(string roomId)
    {
        return $"Snapshot:{roomId}";
    }

    public static string GetRoomEventIdsKey(string batchId)
    {
        return $"RoomEventIds:{batchId}";
    }

    public static string GetTimelineRecordKey(string roomId, string eventId)
    {
        return $"TimelineRecord:{JsonSerializer.Serialize(new[] { roomId, eventId })}";
    }

    public RoomCreators RoomCreators { get; }
    public ImmutableList<string> JoinedRoomIds { get; }
    public ImmutableList<string> LeftRoomIds { get; }
    public StrippedStates Invites { get; }
    public StrippedStates Knocks { get; }
    public string CurrentBatchId { get; }

    public EventStore(
        RoomCreators roomCreators,
        ImmutableList<string> joinedRoomIds,
        ImmutableList<string> leftRoomIds,
        StrippedStates invites,
        StrippedStates knocks,
        string currentBatchId)
    {
        ArgumentNullException.ThrowIfNull(roomCreators);
        ArgumentNullException.ThrowIfNull(joinedRoomIds);
        ArgumentNullException.ThrowIfNull(leftRoomIds);
        ArgumentNullException.ThrowIfNull(invites);
        ArgumentNullException.ThrowIfNull(knocks);
        ArgumentNullException.ThrowIfNull(currentBatchId);

        RoomCreators = roomCreators;
        JoinedRoomIds = joinedRoomIds;
        LeftRoomIds = leftRoomIds;
        Invites = invites;
        Knocks = knocks;
        CurrentBatchId = currentBatchId;
    }

    public async static ValueTask<EventStore> CreateAsync(IKeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var roomCreators = await store.GetDeserializedValueAsync<RoomCreators>(RoomCreatorsKey);
        if (roomCreators is null)
        {
            roomCreators = RoomCreators.Empty;
            await store.PutSerializedValueAsync(RoomCreatorsKey, roomCreators);
        }
        var joinedRoomIds = await store.GetDeserializedValueAsync<ImmutableList<string>>(JoinedRoomIdsKey);
        if (joinedRoomIds is null)
        {
            joinedRoomIds = ImmutableList<string>.Empty;
            await store.PutSerializedValueAsync(JoinedRoomIdsKey, joinedRoomIds);
        }
        var leftRoomIds = await store.GetDeserializedValueAsync<ImmutableList<string>>(LeftRoomIdsKey);
        if (leftRoomIds is null)
        {
            leftRoomIds = ImmutableList<string>.Empty;
            await store.PutSerializedValueAsync(LeftRoomIdsKey, leftRoomIds);
        }
        var invites = await store.GetDeserializedValueAsync<StrippedStates>(InvitesKey);
        if (invites is null)
        {
            invites = StrippedStates.Empty;
            await store.PutSerializedValueAsync(InvitesKey, invites);
        }
        var knocks = await store.GetDeserializedValueAsync<StrippedStates>(KnocksKey);
        if (knocks is null)
        {
            knocks = StrippedStates.Empty;
            await store.PutSerializedValueAsync(KnocksKey, knocks);
        }
        string? currentBatchId = await store.GetStringAsync(CurrentBatchIdKey);
        if (currentBatchId is null)
        {
            currentBatchId = string.Empty;
            await store.PutStringAsync(CurrentBatchIdKey, currentBatchId);
        }
        return new EventStore(roomCreators, joinedRoomIds, leftRoomIds, invites, knocks, currentBatchId); ;
    }

    public async ValueTask<EventStore> SetCreatorAsync(IKeyValueStore store, string roomId, string creator)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(creator);

        var newRoomCreators = RoomCreators.Add(roomId, creator);
        await store.PutSerializedValueAsync(RoomCreatorsKey, newRoomCreators);
        return new EventStore(newRoomCreators, JoinedRoomIds, LeftRoomIds, Invites, Knocks, CurrentBatchId);
    }

    public static ValueTask<PersistentDataUnit?> GetEventAsync(IKeyValueStore store, string roomId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        return store.GetDeserializedValueAsync<PersistentDataUnit>(GetEventKey(roomId, eventId));
    }

    public static ValueTask PutEventAsync(IKeyValueStore store, string roomId, string eventId, PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(pdu);

        return store.PutSerializedValueAsync(GetEventKey(roomId, eventId), pdu);
    }

    public static ValueTask<States?> GetStatesAsync(IKeyValueStore store, string roomId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        string key = GetStatesKey(roomId, eventId);
        return store.GetDeserializedValueAsync<States>(key);
    }

    public static ValueTask PutStatesAsync(IKeyValueStore store, string roomId, string eventId, States states)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(states);

        return store.PutSerializedValueAsync(GetStatesKey(roomId, eventId), states);
    }

    public static async ValueTask<RoomSnapshot> GetRoomSnapshotAsync(IKeyValueStore store, string roomId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);

        var snapshot = await store.GetDeserializedValueAsync<RoomSnapshot>(GetSnapshotKey(roomId));
        return snapshot ?? new RoomSnapshot();
    }

    public static ValueTask PutRoomSnapshotAsync(IKeyValueStore store, string roomId, RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(snapshot);

        return store.PutSerializedValueAsync(GetSnapshotKey(roomId), snapshot);
    }

    public async ValueTask<EventStore> SetJoinedRoomIdsAsync(IKeyValueStore store, ImmutableList<string> joinedRoomIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(joinedRoomIds);

        await store.PutSerializedValueAsync(JoinedRoomIdsKey, joinedRoomIds);
        return new EventStore(RoomCreators, joinedRoomIds, LeftRoomIds, Invites, Knocks, CurrentBatchId);
    }

    public async ValueTask<EventStore> SetLeftRoomIdsAsync(IKeyValueStore store, ImmutableList<string> leftRoomIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(leftRoomIds);

        await store.PutSerializedValueAsync(LeftRoomIdsKey, leftRoomIds);
        return new EventStore(RoomCreators, JoinedRoomIds, leftRoomIds, Invites, Knocks, CurrentBatchId);
    }

    public async ValueTask<EventStore> SetInvitesAsync(IKeyValueStore store, StrippedStates invites)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(invites);

        await store.PutSerializedValueAsync(InvitesKey, invites);
        return new EventStore(RoomCreators, JoinedRoomIds, LeftRoomIds, invites, Knocks, CurrentBatchId);
    }

    public async ValueTask<EventStore> SetKnocksAsync(IKeyValueStore store, StrippedStates knocks)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(knocks);

        await store.PutSerializedValueAsync(KnocksKey, knocks);
        return new EventStore(RoomCreators, JoinedRoomIds, LeftRoomIds, Invites, knocks, CurrentBatchId);
    }

    public async ValueTask<EventStore> SetCurrentBatchIdAsync(IKeyValueStore store, string currentBatchId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(currentBatchId);

        await store.PutStringAsync(CurrentBatchIdKey, currentBatchId);
        return new EventStore(RoomCreators, JoinedRoomIds, LeftRoomIds, Invites, Knocks, currentBatchId);
    }

    public static ValueTask<RoomEventIds?> GetRoomEventIdsAsync(IKeyValueStore store, string batchId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(batchId);

        return store.GetDeserializedValueAsync<RoomEventIds>(GetRoomEventIdsKey(batchId));
    }

    public static ValueTask PutRoomEventIdsAsync(IKeyValueStore store, string batchId, RoomEventIds eventIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(batchId);

        return store.PutSerializedValueAsync(GetRoomEventIdsKey(batchId), eventIds);
    }

    public static ValueTask<TimelineRecord?> GetTimelineRecordAsync(
        IKeyValueStore store,
        string roomId,
        string eventId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        return store.GetDeserializedValueAsync<TimelineRecord>(GetTimelineRecordKey(roomId, eventId));
    }

    public static ValueTask PutTimelineRecordAsync(
        IKeyValueStore store,
        string roomId,
        string eventId,
        TimelineRecord record)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(record);

        return store.PutSerializedValueAsync(GetTimelineRecordKey(roomId, eventId), record);
    }
}
