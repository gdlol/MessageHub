using System.Collections.Immutable;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms.Timeline;

public class BatchStates
{
    public string BatchId { get; init; } = string.Empty;
    public ImmutableList<string> JoinedRoomIds { get; init; } = ImmutableList<string>.Empty;
    public ImmutableList<string> LeftRoomIds { get; init; } = ImmutableList<string>.Empty;
    public ImmutableDictionary<string, ImmutableList<StrippedStateEvent>> Invites { get; init; }
        = ImmutableDictionary<string, ImmutableList<StrippedStateEvent>>.Empty;
    public ImmutableDictionary<string, ImmutableList<StrippedStateEvent>> Knocks { get; init; }
        = ImmutableDictionary<string, ImmutableList<StrippedStateEvent>>.Empty;
    public ImmutableDictionary<string, string> RoomEventIds { get; init; } = ImmutableDictionary<string, string>.Empty;

    public BatchStates Filter(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        return new BatchStates
        {
            BatchId = BatchId,
            JoinedRoomIds = JoinedRoomIds.Where(roomIdFilter).ToImmutableList(),
            LeftRoomIds = includeLeave
                ? LeftRoomIds.Where(roomIdFilter).ToImmutableList()
                : ImmutableList<string>.Empty,
            Invites = Invites.Where(x => roomIdFilter(x.Key)).ToImmutableDictionary(),
            Knocks = Knocks.Where(x => roomIdFilter(x.Key)).ToImmutableDictionary(),
            RoomEventIds = RoomEventIds.Where(x => roomIdFilter(x.Key)).ToImmutableDictionary()
        };
    }
}
