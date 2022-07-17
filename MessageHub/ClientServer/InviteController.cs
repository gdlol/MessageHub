using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class InviteController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IUserDiscoveryService userDiscoveryService;
    private readonly IRemoteRooms remoteRooms;
    private readonly IEventSaver eventSaver;
    private readonly IEventPublisher eventPublisher;

    public InviteController(
        IIdentityService identityService,
        IRooms rooms,
        IUserDiscoveryService userDiscoveryService,
        IRemoteRooms remoteRooms,
        IEventSaver eventSaver,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(userDiscoveryService);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.identityService = identityService;
        this.rooms = rooms;
        this.userDiscoveryService = userDiscoveryService;
        this.remoteRooms = remoteRooms;
        this.eventSaver = eventSaver;
        this.eventPublisher = eventPublisher;
    }

    [Route("rooms/{roomId}/invite")]
    [HttpPost]
    public async Task<IActionResult> Invite([FromRoute] string roomId, [FromBody] InviteRequest request)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var identity = identityService.GetSelfIdentity();
        var sender = UserIdentifier.FromId(identity.Id);
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);

        // Authorize event.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = cts.Token;
        string? avatarUrl;
        string? displayName;
        try
        {
            (avatarUrl, displayName) = await userDiscoveryService.GetUserProfileAsync(request.UserId, token);
        }
        catch (OperationCanceledException)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.NotFound))
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }
        var content = DefaultJsonSerializer.SerializeToElement(
            new MemberEvent
            {
                AvatarUrl = avatarUrl,
                DisplayName = displayName,
                MemberShip = MembershipStates.Invite,
                Reason = request.Reason
            });
        var eventAuthorizer = new EventAuthorizer(roomSnapshot.StateContents);
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: request.UserId,
            sender: sender,
            content: content))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        var (newSnapshot, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.Member,
            stateKey: request.UserId,
            serverKeys: identity.GetServerKeys(),
            sender: sender,
            content: content,
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
        .Where(x => (x.EventType, x.StateKey) != (EventTypes.Member, request.UserId))
        .ToList();
        inviteRoomState.Add(new StrippedStateEvent
        {
            Content = pdu.Content,
            EventType = pdu.EventType,
            Sender = pdu.Sender,
            StateKey = pdu.StateKey!
        });
        var remoteInviteRequest = new Federation.Protocol.InviteRequest
        {
            Event = pdu,
            InviteRoomState = inviteRoomState.ToArray(),
            RoomVersion = 9
        };
        await remoteRooms.InviteAsync(roomId, eventId, remoteInviteRequest);
        await eventSaver.SaveAsync(roomId, eventId, pdu, newSnapshot.States);
        await eventPublisher.PublishAsync(pdu);
        return new JsonResult(new object());
    }
}
