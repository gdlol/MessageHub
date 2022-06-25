using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.Events.General;

public static class ReceiptTypes
{
    public const string Read = "m.read";
}

public class ReadReceiptMetadata
{
    [Required]
    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }
}

public class UserReadReceipt
{
    [Required]
    [JsonPropertyName("data")]
    public ReadReceiptMetadata Data { get; set; } = default!;

    [Required]
    [JsonPropertyName("event_ids")]
    public string[] EventIds { get; set; } = default!;
}

public class RoomReceipts
{
    [Required]
    [JsonPropertyName(ReceiptTypes.Read)]
    public Dictionary<string, UserReadReceipt> ReadReceipts { get; set; } = default!;
}

[EventType(EventType)]
public class ReceiptEvent
{
    public const string EventType = "m.receipt";

    [Required]
    [JsonPropertyName("content")]
    public Dictionary<string, RoomReceipts> Content { get; set; } = default!;

    public static ReceiptEvent Create(string userId, string roomId, string eventId, long timestamp)
    {
        return new ReceiptEvent
        {
            Content = new()
            {
                [roomId] = new RoomReceipts
                {
                    ReadReceipts = new()
                    {
                        [userId] = new UserReadReceipt
                        {
                            Data = new ReadReceiptMetadata
                            {
                                Timestamp = timestamp
                            },
                            EventIds = new[] { eventId }
                        }
                    }
                }
            }
        };
    }

    public EphemeralDataUnit ToEdu()
    {
        return new EphemeralDataUnit
        {
            EventType = EventType,
            Content = JsonSerializer.SerializeToElement(Content, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            })
        };
    }

    public static ReceiptEvent FromEdu(EphemeralDataUnit edu)
    {
        var content = JsonSerializer.Deserialize<Dictionary<string, RoomReceipts>>(edu.Content);
        if (content is null)
        {
            throw new InvalidOperationException();
        }
        return new ReceiptEvent
        {
            Content = content
        };
    }
}
