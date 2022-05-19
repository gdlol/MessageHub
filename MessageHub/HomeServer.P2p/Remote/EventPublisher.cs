using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class EventPublisher : IEventPublisher
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IRequestHandler requestHandler;

    public EventPublisher(IPeerIdentity peerIdentity, IRequestHandler requestHandler)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(requestHandler);

        this.peerIdentity = peerIdentity;
        this.requestHandler = requestHandler;
    }

    public async Task PublishAsync(PersistentDataUnit pdu)
    {
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
        await requestHandler.SendRequest(request);
    }
}
