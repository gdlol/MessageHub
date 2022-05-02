using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.Dummy.Remote;

public class DummyEventPublisher : IEventPublisher
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IRequestHandler requestHandler;

    public DummyEventPublisher(IPeerIdentity peerIdentity, IRequestHandler requestHandler)
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
            requestTarget: $"_matrix/federation/v1/send/{txnId}",
            content: parameters);
        await requestHandler.SendRequest(request);
        // var result = await requestHandler.SendRequest(request);
        // var errors = result.GetProperty("pdus").Deserialize<Dictionary<string, string>>()!;
        // if (errors.Count > 0)
        // {
        //     throw new InvalidOperationException(JsonSerializer.Serialize(errors, new JsonSerializerOptions
        //     {
        //         WriteIndented = true
        //     }));
        // }
    }
}
