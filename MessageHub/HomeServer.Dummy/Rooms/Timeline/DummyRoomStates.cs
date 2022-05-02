using System.Collections.Immutable;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public class DummyRoomStates : IRoomStates
{
    public string BatchId { get; init; } = string.Empty;

    public ImmutableList<string> InvitedRoomIds { get; init; } = ImmutableList<string>.Empty;

    public ImmutableList<string> JoinedRoomIds { get; init; } = ImmutableList<string>.Empty;

    public ImmutableList<string> KnockedRoomIds { get; init; } = ImmutableList<string>.Empty;

    public ImmutableList<string> LeftRoomIds { get; init; } = ImmutableList<string>.Empty;

    public ImmutableDictionary<string, string> RoomEventIds { get; init; } = ImmutableDictionary<string, string>.Empty;

    public ImmutableDictionary<string, ImmutableList<StrippedStateEvent>> StrippedStateEvents { get; init; }
        = ImmutableDictionary<string, ImmutableList<StrippedStateEvent>>.Empty;

    IReadOnlyList<string> IRoomStates.InvitedRoomIds => InvitedRoomIds;

    IReadOnlyList<string> IRoomStates.JoinedRoomIds => JoinedRoomIds;

    IReadOnlyList<string> IRoomStates.KnockedRoomIds => KnockedRoomIds;

    IReadOnlyList<string> IRoomStates.LeftRoomIds => LeftRoomIds;

    IReadOnlyDictionary<string, string> IRoomStates.RoomEventIds => RoomEventIds;

    public StrippedStateEvent[] GetStrippedStateEvents(string roomId)
    {
        return StrippedStateEvents[roomId].ToArray();
    }

    public DummyRoomStates Filter(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        return new DummyRoomStates
        {
            BatchId = BatchId,
            InvitedRoomIds = InvitedRoomIds.Where(roomIdFilter).ToImmutableList(),
            JoinedRoomIds = JoinedRoomIds.Where(roomIdFilter).ToImmutableList(),
            KnockedRoomIds = KnockedRoomIds.Where(roomIdFilter).ToImmutableList(),
            LeftRoomIds = includeLeave
                ? LeftRoomIds.Where(roomIdFilter).ToImmutableList()
                : ImmutableList<string>.Empty,
            StrippedStateEvents = StrippedStateEvents
                .Where(x => 
                {
                    if (includeLeave)
                    {
                        return roomIdFilter(x.Key);
                    }
                    else
                    {
                        return roomIdFilter(x.Key) && !LeftRoomIds.Contains(x.Key);
                    }
                })
                .ToImmutableDictionary()
        };
    }
}
