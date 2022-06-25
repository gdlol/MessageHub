using System.Collections.Immutable;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.HomeServer;

public interface IUserReadReceipts
{
    void PutReceipt(string roomId, string userId, string receiptType, UserReadReceipt readReceipt);
    ImmutableDictionary<(string userId, string receiptType), UserReadReceipt> TakeReceipts(string roomId);
}
