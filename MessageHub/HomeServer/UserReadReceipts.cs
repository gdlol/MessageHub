using System.Collections.Concurrent;
using System.Collections.Immutable;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.HomeServer;
using RoomReceipts = ImmutableDictionary<(string userId, string receiptType), UserReadReceipt>;

public class UserReadReceipts : IUserReadReceipts
{
    private readonly ConcurrentDictionary<string, RoomReceipts> receipts = new();

    public void PutReceipt(string roomId, string userId, string receiptType, UserReadReceipt readReceipt)
    {
        receipts.AddOrUpdate(
            roomId,
            _ => RoomReceipts.Empty.Add((userId, receiptType), readReceipt),
            (_, roomReceipts) => roomReceipts.SetItem((userId, receiptType), readReceipt));
    }

    public RoomReceipts TakeReceipts(string roomId)
    {
        if (receipts.TryRemove(roomId, out var roomReceipts))
        {
            return roomReceipts;
        }
        return RoomReceipts.Empty;
    }
}
