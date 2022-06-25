using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Remote;

namespace MessageHub.HomeServer.P2p.Remote;

public class EventPublisher : IEventPublisher
{
    private readonly IIdentityService identityService;
    private readonly IRequestHandler requestHandler;
    private readonly IUserPresence userPresence;

    public EventPublisher(IIdentityService identityService, IRequestHandler requestHandler, IUserPresence userPresence)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(requestHandler);
        ArgumentNullException.ThrowIfNull(userPresence);

        this.identityService = identityService;
        this.requestHandler = requestHandler;
        this.userPresence = userPresence;
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
        var edus = new List<EphemeralDataUnit>();
        if (UserIdentifier.FromId(identity.Id).ToString() == pdu.Sender)
        {
            var presenceStatus = new PresenceStatus { Presence = PresenceValues.Online };
            userPresence.SetPresence(pdu.Sender, presenceStatus.Presence, null);
            edus.Add(new EphemeralDataUnit
            {
                EventType = PresenceEvent.EventType,
                Content = JsonSerializer.SerializeToElement(new PresenceUpdate
                {
                    Push = new[]
                    {
                        UserPresenceUpdate.Create(pdu.Sender, presenceStatus)
                    }
                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })
            });
        }
        if (edus.Count > 0)
        {
            parameters.Edus = edus.ToArray();
        }
        var request = identity.SignRequest(
            destination: pdu.RoomId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v1/send/{txnId}",
            content: parameters);
        await requestHandler.SendRequest(request);
    }

    public async Task PublishAsync(string roomId, EphemeralDataUnit edu)
    {
        var identity = identityService.GetSelfIdentity();
        string txnId = Guid.NewGuid().ToString();
        var parameters = new PushMessagesRequest
        {
            Origin = identity.Id,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Pdus = Array.Empty<PersistentDataUnit>(),
            Edus = new[] { edu }
        };
        var request = identity.SignRequest(
            destination: roomId,
            requestMethod: HttpMethods.Put,
            requestTarget: $"/_matrix/federation/v1/send/{txnId}",
            content: parameters);
        await requestHandler.SendRequest(request);
    }
}
