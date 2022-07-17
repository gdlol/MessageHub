using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;
using MessageHub.Serialization;

namespace MessageHub.ClientServer.Sync;
using UserReceipts = Dictionary<string, ReadReceiptMetadata>;
using EventReceipts = Dictionary<string, Dictionary<string, ReadReceiptMetadata>>;

public class EphemeralLoader
{
    private readonly IUserReceipts userReceipts;

    public EphemeralLoader(IUserReceipts userReceipts)
    {
        ArgumentNullException.ThrowIfNull(userReceipts);

        this.userReceipts = userReceipts;
    }

    public async ValueTask<Ephemeral?> LoadEphemeralEventsAsync(string roomId, RoomEventFilter? filter)
    {
        if (!filter.ShouldIncludeRoomId(roomId))
        {
            return null;
        }

        var receipts = await userReceipts.TakeReceiptsAsync(roomId);
        if (receipts.IsEmpty)
        {
            return null;
        }
        var content = new Dictionary<string, EventReceipts>();
        foreach (var ((userId, receiptType), receipt) in receipts)
        {
            if (!filter.ShouldIncludeEvent(userId, ReceiptEvent.EventType))
            {
                continue;
            }
            foreach (string eventId in receipt.EventIds)
            {
                if (!content.ContainsKey(eventId))
                {
                    content[eventId] = new EventReceipts();
                }
                if (!content[eventId].ContainsKey(receiptType))
                {
                    content[eventId][receiptType] = new UserReceipts();
                }
                content[eventId][receiptType][userId] = receipt.Data;
            }
        }
        return new Ephemeral
        {
            Events = new[]
            {
                new Event
                {
                    Content = DefaultJsonSerializer.SerializeToElement(content),
                    EventType = ReceiptEvent.EventType
                }
            }
        };
    }
}
