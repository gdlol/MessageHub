using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.Events;

namespace MessageHub.ClientServer.Protocol;

public class ClientEventWithoutRoomID
{
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; } = default!;

    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = default!;

    [JsonPropertyName("origin_server_ts")]
    public long OriginServerTimestamp { get; set; }

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = default!;

    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [JsonPropertyName("type")]
    public string EventType { get; set; } = default!;

    [JsonPropertyName("unsigned")]
    public JsonElement? Unsigned { get; set; }

    public ClientEvent ToClientEvent(string roomId)
    {
        return new ClientEvent
        {
            Content = Content,
            EventId = EventId,
            OriginServerTimestamp = OriginServerTimestamp,
            RoomId = roomId,
            Sender = Sender,
            StateKey = StateKey,
            EventType = EventType,
            Unsigned = Unsigned
        };
    }

    public static ClientEventWithoutRoomID FromPersistentDataUnit(PersistentDataUnit pdu)
    {
        return new ClientEventWithoutRoomID
        {
            Content = pdu.Content,
            EventId = EventHash.GetEventId(pdu),
            OriginServerTimestamp = pdu.OriginServerTimestamp,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey,
            EventType = pdu.EventType,
            Unsigned = pdu.Unsigned
        };
    }
}
