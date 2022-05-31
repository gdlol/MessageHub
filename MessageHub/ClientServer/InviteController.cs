using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class InviteController : ControllerBase
{
    public class InviteParameters
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [Required]
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = default!;
    }

    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IRemoteRooms remoteRooms;
    private readonly IEventSaver eventSaver;
    private readonly IEventPublisher eventPublisher;

    public InviteController(
        IIdentityService identityService,
        IRooms rooms,
        IRemoteRooms remoteRooms,
        IEventSaver eventSaver,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.identityService = identityService;
        this.rooms = rooms;
        this.remoteRooms = remoteRooms;
        this.eventSaver = eventSaver;
        this.eventPublisher = eventPublisher;
    }

    [Route("rooms/{roomId}/invite")]
    [HttpPost]
    public async Task<IActionResult> Invite([FromRoute] string roomId, [FromBody] InviteParameters parameters)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var identity = identityService.GetSelfIdentity();
        var senderId = UserIdentifier.FromId(identity.Id);
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);

        // Authorize event.
        var eventAuthorizer = new EventAuthorizer(roomSnapshot.StateContents);
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: parameters.UserId,
            sender: senderId,
            content: JsonSerializer.SerializeToElement(
                new MemberEvent
                {
                    MemberShip = MembershipStates.Invite,
                    Reason = parameters.Reason
                },
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        var (newSnapshot, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.Member,
            stateKey: parameters.UserId,
            serverKeys: identity.GetServerKeys(),
            sender: senderId,
            content: JsonSerializer.SerializeToElement(
                new MemberEvent { MemberShip = MembershipStates.Invite },
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        pdu = identity.SignEvent(pdu);
        string eventId = EventHash.GetEventId(pdu);

        // Remote invite
        var stateEvents = await roomEventStore.LoadStateEventsAsync(roomSnapshot.LatestEventIds[0]);
        var inviteRoomState = stateEvents.Values.Select(pdu => new StrippedStateEvent
        {
            Content = pdu.Content,
            EventType = pdu.EventType,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey!
        })
        .Where(x => (x.EventType, x.StateKey) != (EventTypes.Member, parameters.UserId))
        .ToList();
        inviteRoomState.Add(new StrippedStateEvent
        {
            Content = pdu.Content,
            EventType = pdu.EventType,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey!
        });
        var remoteInviteParameters = new Federation.Protocol.InviteParameters
        {
            Event = pdu,
            InviteRoomState = inviteRoomState.ToArray(),
            RoomVersion = 9
        };
        await remoteRooms.InviteAsync(roomId, eventId, remoteInviteParameters);
        await eventSaver.SaveAsync(roomId, eventId, pdu, newSnapshot.States);
        await eventPublisher.PublishAsync(pdu);
        return new JsonResult(new object());
    }
}
