using System.Text.Json;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.Dummy.Remote;

public class DummyEventPublisher : IEventPublisher
{
    private readonly ILogger logger;
    private readonly IPeerIdentity peerIdentity;
    private readonly IRequestHandler requestHandler;

    public DummyEventPublisher(
        ILogger<DummyEventPublisher> logger,
        IPeerIdentity peerIdentity,
        IRequestHandler requestHandler)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(requestHandler);

        this.logger = logger;
        this.peerIdentity = peerIdentity;
        this.requestHandler = requestHandler;
    }

    public async Task PublishAsync(PersistentDataUnit pdu)
    {
        string eventId = EventHash.GetEventId(pdu);
        string txnId = Guid.NewGuid().ToString();
        var parameters = new PushMessagesRequest
        {
            Origin = peerIdentity.Id,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Pdus = new[] { pdu }
        };
        var request = peerIdentity.SignRequest(
            destination: pdu.RoomId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v1/send/{txnId}",
            content: parameters);
        var result = await requestHandler.SendRequest(request);
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("pdus", out var element))
        {
            try
            {
                var pdus = element.Deserialize<Dictionary<string, JsonElement>>();
                if (pdus?.TryGetValue(eventId, out var feedback) == true
                    && feedback.TryGetProperty("error", out var error))
                {
                    logger.LogError("Error sending event {eventId}: {error}, {pdu}", eventId, error, pdu);
                }
            }
            catch (Exception)
            {
                logger.LogError("Publish response: {}", element);
            }
        }
    }
}
