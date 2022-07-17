using System.Text.Json;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Events.Room;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Presence;

internal class SelfPresencePublisher
{
    private readonly PresenceServiceContext context;

    public SelfPresencePublisher(PresenceServiceContext context)
    {
        this.context = context;
    }

    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identity = context.IdentityService.GetSelfIdentity();
        string userId = UserIdentifier.FromId(identity.Id).ToString();
        var presenceStatus = context.UserPresence.GetPresence(userId);
        if (presenceStatus is null)
        {
            presenceStatus = new PresenceStatus
            {
                Presence = PresenceValues.Unavailable
            };
        }
        var edu = new EphemeralDataUnit
        {
            EventType = PresenceEvent.EventType,
            Content = DefaultJsonSerializer.SerializeToElement(new PresenceUpdate
            {
                Push = new[]
                {
                    UserPresenceUpdate.Create(userId, presenceStatus)
                }
            })
        };
        string txnId = Guid.NewGuid().ToString();
        var request = new PushMessagesRequest
        {
            Origin = identity.Id,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Pdus = Array.Empty<PersistentDataUnit>(),
            Edus = new[] { edu }
        };

        var batchStates = await context.TimelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await context.Rooms.GetRoomSnapshotAsync(roomId);
            if (!snapshot.StateContents.TryGetValue(
                new RoomStateKey(EventTypes.JoinRules, string.Empty),
                out var content))
            {
                continue;
            }
            var joinRules = content.Deserialize<JoinRulesEvent>()!;
            if (joinRules.JoinRule != JoinRules.Invite)
            {
                continue;
            }
            var signedRequest = identity.SignRequest(
                destination: roomId,
                requestMethod: HttpMethods.Put,
                requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                content: request);
            context.PublishEventNotifier.Notify(new(roomId, signedRequest.ToJsonElement()));
        }
    }
}
