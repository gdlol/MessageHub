using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.RoomVersions.V9;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class KnockRoomController : ControllerBase
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IPeerStore peerStore;
    private readonly IRooms rooms;
    private readonly IEventReceiver eventReceiver;
    private readonly IEventPublisher eventPublisher;

    public KnockRoomController(
        IPeerIdentity peerIdentity,
        IPeerStore peerStore,
        IRooms rooms,
        IEventReceiver eventReceiver,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(peerStore);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventReceiver);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.peerIdentity = peerIdentity;
        this.peerStore = peerStore;
        this.rooms = rooms;
        this.eventReceiver = eventReceiver;
        this.eventPublisher = eventPublisher;
    }

    [Route("make_knock/{roomId}/{userId}")]
    [HttpGet]
    public async Task<IActionResult> MakeKnock(string roomId, string userId)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        if (!peerStore.TryGetPeer(request.Origin, out var senderIdentity))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.BadState));
        }
        var userIdentifier = UserIdentifier.FromId(request.Origin);
        if (userIdentifier.ToString() != userId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(userId)));
        }
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var room = await rooms.GetRoomAsync(roomId);
        var eventAuthorizer = new EventAuthorizer(RoomIdentifier.Parse(roomId), room.StateContents);
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: userId,
            sender: UserIdentifier.FromId(peerIdentity.Id),
            content: new MemberEvent
            {
                MemberShip = MembershipStates.Knock
            }))
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
        var eventCreator = new EventCreator(
            new Dictionary<string, IRoom>
            {
                [roomId] = room
            }.ToImmutableDictionary(),
            senderIdentity);
        var result = await eventCreator.CreateEventJsonAsync(
            roomId,
            EventTypes.Member,
            userId,
            JsonSerializer.SerializeToElement(
                new MemberEvent { MemberShip = MembershipStates.Knock },
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return new JsonResult(result);
    }

    [Route("send_knock/{roomId}/{eventId}")]
    [HttpPut]
    public async Task<IActionResult> SendKnock(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromBody] PersistentDataUnit pdu)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var room = await rooms.GetRoomAsync(roomId);
        var userIdentifier = UserIdentifier.FromId(request.Origin);
        var eventAuthorizer = new EventAuthorizer(RoomIdentifier.Parse(roomId), room.StateContents);
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: userIdentifier.ToString(),
            sender: UserIdentifier.FromId(peerIdentity.Id),
            content: new MemberEvent
            {
                MemberShip = MembershipStates.Knock
            }))
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
        if (errors.TryGetValue(eventId, out string? error))
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
