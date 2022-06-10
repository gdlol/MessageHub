using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class KnockRoomController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IEventReceiver eventReceiver;
    private readonly IEventPublisher eventPublisher;

    public KnockRoomController(
        IIdentityService identityService,
        IRooms rooms,
        IEventReceiver eventReceiver,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventReceiver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.identityService = identityService;
        this.rooms = rooms;
        this.eventReceiver = eventReceiver;
        this.eventPublisher = eventPublisher;
    }

    [Route("make_knock/{roomId}/{userId}")]
    [HttpGet]
    public async Task<IActionResult> MakeKnock(string roomId, string userId)
    {
        var identity = identityService.GetSelfIdentity();
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var senderId = UserIdentifier.FromId(request.Origin);
        if (senderId.ToString() != userId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var eventAuthorizer = new EventAuthorizer(roomSnapshot.StateContents);
        var knockContent = JsonSerializer.SerializeToElement(
            new MemberEvent { MemberShip = MembershipStates.Knock },
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: userId,
            sender: senderId,
            content: knockContent))
        {
            if (eventAuthorizer.TryGetJoinRulesEvent()?.JoinRule == JoinRules.Knock)
            {
                return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
            else
            {
                return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
            }
        }
        var (_, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.Member,
            serverKeys: identity.GetServerKeys(),
            stateKey: userId,
            sender: senderId,
            content: knockContent,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return new JsonResult(pdu);
    }

    [Route("send_knock/{roomId}/{eventId}")]
    [HttpPut]
    public async Task<IActionResult> SendKnock(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromBody] PersistentDataUnit pdu)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var sender = UserIdentifier.FromId(request.Origin);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        if (pdu.RoomId != roomId
            || EventHash.TryGetEventId(pdu) != eventId
            || pdu.EventType != EventTypes.Member
            || pdu.Sender != sender.ToString()
            || pdu.StateKey != pdu.Sender)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }
        try
        {
            var memberEvent = pdu.Content.Deserialize<MemberEvent>();
            if (memberEvent?.MemberShip != MembershipStates.Knock)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter));
            }
        }
        catch (Exception)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var eventAuthorizer = new EventAuthorizer(roomSnapshot.StateContents);        
        if (!eventAuthorizer.Authorize(pdu.EventType, pdu.StateKey, sender, pdu.Content))
        {
            if (eventAuthorizer.TryGetJoinRulesEvent()?.JoinRule == JoinRules.Knock)
            {
                return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
            else
            {
                return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
            }
        }
        if (string.IsNullOrEmpty(eventId) || EventHash.TryGetEventId(pdu) != eventId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(eventId)));
        }
        var errors = await eventReceiver.ReceivePersistentEventsAsync(new[] { pdu });
        if (errors.TryGetValue(eventId, out string? error) && !string.IsNullOrEmpty(error))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.BadState, error));
        }
        await eventPublisher.PublishAsync(pdu);
        return new JsonResult(new
        {
            knock_room_state = Array.Empty<string>()
        });
    }
}
