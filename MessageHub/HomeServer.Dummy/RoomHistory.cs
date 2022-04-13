using System.Collections.Immutable;

namespace MessageHub.HomeServer.Dummy;

internal static class RoomHistory
{
    public static ImmutableList<RoomStates> RoomStatesList { get; private set; }

    private static ImmutableDictionary<string, RoomStates> batchIdIndex;
    private readonly static object locker;

    static RoomHistory()
    {
        RoomStatesList = ImmutableList<RoomStates>.Empty.Add(RoomStates.Empty);
        batchIdIndex = ImmutableDictionary<string, RoomStates>.Empty.Add(string.Empty, RoomStatesList[0]);
        locker = new object();
    }

    public static RoomStates? TryGetRoomStates(string batchId)
    {
        return batchIdIndex.TryGetValue(batchId, out var roomStates) ? roomStates : null;
    }

    public static void AddEvent(PersistentDataUnit pdu, RoomMembership? newMembership)
    {
        lock (locker)
        {
            var roomStates = RoomStatesList[^1];
            var membership = newMembership ??= roomStates.Rooms[pdu.RoomId].Membership;
            var newRoomStates = roomStates.AddEvent(pdu, membership);
            RoomStatesList = RoomStatesList.Add(newRoomStates);
            batchIdIndex = batchIdIndex.SetItem(newRoomStates.BatchId, newRoomStates);
        }
    }
}
