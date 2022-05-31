using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class EventPublisher : IEventPublisher
{
    private readonly IIdentityService identityService;
    private readonly IRequestHandler requestHandler;

    public EventPublisher(IIdentityService identityService, IRequestHandler requestHandler)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(requestHandler);

        this.identityService = identityService;
        this.requestHandler = requestHandler;
    }

    public async Task PublishAsync(PersistentDataUnit pdu)
    {
        var identity = identityService.GetSelfIdentity();
        string txnId = Guid.NewGuid().ToString();
        var parameters = new PushMessagesRequest
        {
            Origin = identity.Id,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Pdus = new[] { pdu }
        };
        var request = identity.SignRequest(
            destination: pdu.RoomId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v1/send/{txnId}",
            content: parameters);
        await requestHandler.SendRequest(request);
    }
}
