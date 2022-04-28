using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class InviteController : ControllerBase
{
    public class InviteParameters
    {
        [Required]
        [JsonPropertyName("event")]
        public PersistentDataUnit Event { get; set; } = default!;

        [JsonPropertyName("invite_room_state")]
        public StrippedStateEvent[]? InviteRoomState { get; set; }

        [Required]
        [JsonPropertyName("room_version")]
        public int RoomVersion { get; set; }
    }

    private readonly IPeerIdentity peerIdentity;
    private readonly IPeerStore peerStore;
    private readonly IRooms rooms;
    private readonly IEventReceiver eventReceiver;
    private readonly IEventPublisher eventPublisher;

    public InviteController(
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

    [Route("invite/{roomId}/{eventId}")]
    [HttpPut]
    public IActionResult Invite(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromBody] InviteParameters parameters)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var pdu = parameters.Event;
        if (string.IsNullOrEmpty(roomId) || roomId != pdu.RoomId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(roomId)));
        }
        string? computedEventId = EventHash.TryGetEventId(pdu);
        if (computedEventId is null || computedEventId != eventId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(eventId)));
        }
        if (pdu.EventType != EventTypes.Member)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.EventType)));
        }
        if (pdu.StateKey != UserIdentifier.FromId(peerIdentity.Id).ToString())
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.StateKey)));
        }
        if (pdu.Origin != request.Origin)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.Origin)));
        }
        var userIdentifier = UserIdentifier.FromId(request.Origin);
        if (userIdentifier.ToString() != pdu.Sender)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.Sender)));
        }
        try
        {
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(pdu.Content);
            if (memberEvent is null || memberEvent.MemberShip != MembershipStates.Invite)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.Content)));
            }
        }
        catch (Exception)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.Content)));
        }
        var signedEvent = peerIdentity.SignJson(pdu.ToJsonElement());
        return new JsonResult(new Dictionary<string, object>
        {
            ["event"] = signedEvent
        });
    }
}
