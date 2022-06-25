using System.Collections.Immutable;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.HomeServer;
using RoomReceipts = ImmutableDictionary<(string userId, string receiptType), UserReadReceipt>;

public interface IUserReceipts
{
    ValueTask PutReceiptAsync(string roomId, string userId, string receiptType, UserReadReceipt readReceipt);
    ValueTask<RoomReceipts> TakeReceiptsAsync(string roomId);
}
