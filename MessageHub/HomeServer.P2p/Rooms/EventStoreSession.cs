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

internal class EventStoreSession : IDisposable
{
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

    private readonly IKeyValueStore store;

    public EventStoreState State { get; private set; }

    public bool IsReadOnly { get; }

    internal EventStoreSession(IKeyValueStore store, EventStoreState state, bool isReadOnly)
    {
        this.store = store;
        State = state;
        IsReadOnly = isReadOnly;
    }

    internal static async ValueTask<EventStoreState> LoadStateAsync(IKeyValueStore store)
    {
        var roomCreators = await store.GetDeserializedValueAsync<RoomCreators>(RoomCreatorsKey);
        var joinedRoomIds = await store.GetDeserializedValueAsync<ImmutableList<string>>(JoinedRoomIdsKey);
        var leftRoomIds = await store.GetDeserializedValueAsync<ImmutableList<string>>(LeftRoomIdsKey);
        var invites = await store.GetDeserializedValueAsync<StrippedStates>(InvitesKey);
        var knocks = await store.GetDeserializedValueAsync<StrippedStates>(KnocksKey);
        string? currentBatchId = await store.GetStringAsync(CurrentBatchIdKey);
        if (currentBatchId is null)
        {
            currentBatchId = EventStoreState.EmptyBatchId;
            await store.PutSerializedValueAsync(GetRoomEventIdsKey(currentBatchId), RoomEventIds.Empty);
            await store.CommitAsync();
        }
        return new EventStoreState
        {
            RoomCreators = roomCreators ?? RoomCreators.Empty,
            JoinedRoomIds = joinedRoomIds ?? ImmutableList<string>.Empty,
            LeftRoomIds = leftRoomIds ?? ImmutableList<string>.Empty,
            Invites = invites ?? StrippedStates.Empty,
            Knocks = knocks ?? StrippedStates.Empty,
            CurrentBatchId = currentBatchId
        };
    }

    public void Dispose()
    {
        store.Dispose();
    }

    public async ValueTask SetRoomCreatorsAsync(RoomCreators roomCreators)
    {
        ArgumentNullException.ThrowIfNull(roomCreators);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutSerializedValueAsync(RoomCreatorsKey, roomCreators);
        State = State with { RoomCreators = roomCreators };
    }

    public async ValueTask SetJoinedRoomIdsAsync(ImmutableList<string> joinedRoomIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(joinedRoomIds);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutSerializedValueAsync(JoinedRoomIdsKey, joinedRoomIds);
        State = State with { JoinedRoomIds = joinedRoomIds };
    }

    public async ValueTask SetLeftRoomIdsAsync(ImmutableList<string> leftRoomIds)
    {
        ArgumentNullException.ThrowIfNull(leftRoomIds);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutSerializedValueAsync(LeftRoomIdsKey, leftRoomIds);
        State = State with { LeftRoomIds = leftRoomIds };
    }

    public async ValueTask SetInvitesAsync(StrippedStates invites)
    {
        ArgumentNullException.ThrowIfNull(invites);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutSerializedValueAsync(InvitesKey, invites);
        State = State with { Invites = invites };
    }

    public async ValueTask SetKnocksAsync(StrippedStates knocks)
    {
        ArgumentNullException.ThrowIfNull(knocks);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutSerializedValueAsync(KnocksKey, knocks);
        State = State with { Knocks = knocks };
    }

    public async ValueTask SetCurrentBatchIdAsync(string currentBatchId)
    {
        ArgumentNullException.ThrowIfNull(currentBatchId);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        await store.PutStringAsync(CurrentBatchIdKey, currentBatchId);
        State = State with { CurrentBatchId = currentBatchId };
    }

    public ValueTask<PersistentDataUnit?> GetEventAsync(string roomId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        return store.GetDeserializedValueAsync<PersistentDataUnit>(GetEventKey(roomId, eventId));
    }

    public ValueTask PutEventAsync(string roomId, string eventId, PersistentDataUnit pdu)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(pdu);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        return store.PutSerializedValueAsync(GetEventKey(roomId, eventId), pdu);
    }

    public ValueTask<States?> GetStatesAsync(string roomId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        string key = GetStatesKey(roomId, eventId);
        return store.GetDeserializedValueAsync<States>(key);
    }

    public ValueTask PutStatesAsync(string roomId, string eventId, States states)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(states);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        return store.PutSerializedValueAsync(GetStatesKey(roomId, eventId), states);
    }

    public async ValueTask<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        var snapshot = await store.GetDeserializedValueAsync<RoomSnapshot>(GetSnapshotKey(roomId));
        return snapshot ?? new RoomSnapshot();
    }

    public ValueTask PutRoomSnapshotAsync(string roomId, RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        return store.PutSerializedValueAsync(GetSnapshotKey(roomId), snapshot);
    }

    public ValueTask<RoomEventIds?> GetRoomEventIdsAsync(string batchId)
    {
        ArgumentNullException.ThrowIfNull(batchId);

        return store.GetDeserializedValueAsync<RoomEventIds>(GetRoomEventIdsKey(batchId));
    }

    public ValueTask PutRoomEventIdsAsync(string batchId, RoomEventIds eventIds)
    {
        ArgumentNullException.ThrowIfNull(batchId);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        return store.PutSerializedValueAsync(GetRoomEventIdsKey(batchId), eventIds);
    }

    public ValueTask<TimelineRecord?> GetTimelineRecordAsync(string roomId, string eventId)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);

        return store.GetDeserializedValueAsync<TimelineRecord>(GetTimelineRecordKey(roomId, eventId));
    }

    public ValueTask PutTimelineRecordAsync(string roomId, string eventId, TimelineRecord record)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(record);
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{nameof(IsReadOnly)}: {IsReadOnly}");
        }

        return store.PutSerializedValueAsync(GetTimelineRecordKey(roomId, eventId), record);
    }
}
