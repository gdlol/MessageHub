using System.Collections.Immutable;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public static class DummyTimeline
{
    public static ImmutableList<string> BatchIds { get; private set; }
    public static ImmutableDictionary<string, ImmutableList<string>> Timelines { get; private set; }
    public static ImmutableDictionary<string, ImmutableDictionary<string, string>> RoomEventIds { get; private set; }
    public static DummyRoomStates RoomStates { get; private set; }

    static DummyTimeline()
    {
        BatchIds = ImmutableList<string>.Empty;
        Timelines = ImmutableDictionary<string, ImmutableList<string>>.Empty;
        RoomEventIds = ImmutableDictionary<string, ImmutableDictionary<string, string>>.Empty;
        RoomStates = new DummyRoomStates();
    }

    public static void AddBatch(
        IReadOnlyDictionary<string, string[]> newEventIds,
        IReadOnlyDictionary<string, ImmutableList<StrippedStateEvent>> invitedRooms,
        ImmutableList<string> joinedRooms,
        IReadOnlyDictionary<string, ImmutableList<StrippedStateEvent>> knockedRooms,
        ImmutableList<string> leftRooms)
    {
        string newBatchId = Guid.NewGuid().ToString();
        BatchIds = BatchIds.Add(newBatchId);
        var roomEventIdsMap = ImmutableDictionary.CreateBuilder<string, string>();
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
        foreach (var (roomId, eventIdList) in Timelines)
        {
            roomEventIdsMap[roomId] = eventIdList[^1];
        }
        RoomEventIds = RoomEventIds.SetItem(newBatchId, roomEventIdsMap.ToImmutable());
        RoomStates = new DummyRoomStates
        {
            BatchId = newBatchId,
            InvitedRoomIds = invitedRooms.Keys.ToImmutableList(),
            JoinedRoomIds = joinedRooms,
            KnockedRoomIds = knockedRooms.Keys.ToImmutableList(),
            LeftRoomIds = leftRooms,
            RoomEventIds = RoomEventIds[newBatchId],
            StrippedStateEvents = invitedRooms.Union(knockedRooms).ToImmutableDictionary()
        };
    }
}
