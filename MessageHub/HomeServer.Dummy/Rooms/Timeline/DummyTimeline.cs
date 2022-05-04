using System.Collections.Immutable;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public static class DummyTimeline
{
    public static ImmutableList<string> BatchIds { get; private set; }
    public static ImmutableDictionary<string, ImmutableList<string>> Timelines { get; private set; }
    public static ImmutableDictionary<string, ImmutableDictionary<string, string>> RoomEventIds { get; private set; }
    public static BatchStates BatchStates { get; private set; }

    static DummyTimeline()
    {
        BatchIds = ImmutableList<string>.Empty;
        Timelines = ImmutableDictionary<string, ImmutableList<string>>.Empty;
        RoomEventIds = ImmutableDictionary<string, ImmutableDictionary<string, string>>.Empty;
        BatchStates = new BatchStates();
    }

    public static void AddBatch(
        IReadOnlyDictionary<string, string[]> newEventIds,
        ImmutableList<string> joinedRoomIds,
        ImmutableList<string> leftRoomIds,
        IReadOnlyDictionary<string, ImmutableList<StrippedStateEvent>> invites,
        IReadOnlyDictionary<string, ImmutableList<StrippedStateEvent>> knocks)
    {
        string newBatchId = Guid.NewGuid().ToString();
        BatchIds = BatchIds.Add(newBatchId);
        foreach (var (roomId, eventIdList) in newEventIds)
        {
            if (Timelines.TryGetValue(roomId, out var existingEventIds))
            {
                Timelines = Timelines.SetItem(roomId, existingEventIds.AddRange(eventIdList));
            }
            else
            {
                Timelines = Timelines.SetItem(roomId, eventIdList.ToImmutableList());
            }
        }
        var roomEventIdsMap = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var (roomId, eventIdList) in Timelines)
        {
            roomEventIdsMap[roomId] = eventIdList[^1];
        }
        RoomEventIds = RoomEventIds.SetItem(newBatchId, roomEventIdsMap.ToImmutable());
        BatchStates = new BatchStates
        {
            BatchId = newBatchId,
            JoinedRoomIds = joinedRoomIds,
            LeftRoomIds = leftRoomIds,
            Invites = invites.ToImmutableDictionary(),
            Knocks = knocks.ToImmutableDictionary(),
            RoomEventIds = RoomEventIds[newBatchId]
        };
    }
}
