using System.Collections.Immutable;

namespace MessageHub.HomeServer.Dummy;

internal static class RoomHistory
{
    public static ImmutableList<RoomStates> RoomStates { get; private set; }
    private readonly static ImmutableDictionary<string, RoomStates> batchIdIndex;

    static RoomHistory()
    {
        RoomStates = ImmutableList<RoomStates>.Empty;
        batchIdIndex = ImmutableDictionary<string, RoomStates>.Empty;
    }

    public static RoomStates? TryGetRoomStates(string batchId)
    {
        return batchIdIndex.TryGetValue(batchId, out var roomStates) ? roomStates : null;
    }
}
