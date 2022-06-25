using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events.General;

namespace MessageHub.ClientServer.Sync;
using UserReceipts = Dictionary<string, ReadReceiptMetadata>;
using EventReceipts = Dictionary<string, Dictionary<string, ReadReceiptMetadata>>;

public class EphemeralLoader
{
    private readonly IUserReadReceipts userReadReceipts;

    public EphemeralLoader(IUserReadReceipts userReadReceipts)
    {
        ArgumentNullException.ThrowIfNull(userReadReceipts);

        this.userReadReceipts = userReadReceipts;
    }

    public Ephemeral? LoadEphemeralEvents(string roomId, RoomEventFilter? filter)
    {
        if (!filter.ShouldIncludeRoomId(roomId))
        {
            return null;
        }

        var receipts = userReadReceipts.TakeReceipts(roomId);
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
                    Content = JsonSerializer.SerializeToElement(content),
                    EventType = ReceiptEvent.EventType
                }
            }
        };
    }
}
