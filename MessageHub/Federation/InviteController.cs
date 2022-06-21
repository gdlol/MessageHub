using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class InviteController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IEventSaver eventSaver;

    public InviteController(IIdentityService identityService, IRooms rooms, IEventSaver eventSaver)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(eventSaver);

        this.identityService = identityService;
        this.rooms = rooms;
        this.eventSaver = eventSaver;
    }

    [Route("invite/{roomId}/{eventId}")]
    [HttpPut]
    public async Task<IActionResult> Invite(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromBody] InviteParameters parameters)
    {
        var identity = identityService.GetSelfIdentity();
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        var pdu = parameters.Event;
        if (string.IsNullOrEmpty(roomId) || roomId != pdu.RoomId)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(roomId)));
        }
        if (rooms.HasRoom(roomId))
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
        if (pdu.StateKey != UserIdentifier.FromId(identity.Id).ToString())
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(pdu.StateKey)));
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
        await eventSaver.SaveInviteAsync(roomId, parameters.InviteRoomState);

        var signedEvent = identity.SignEvent(pdu);
        return new JsonResult(new Dictionary<string, object>
        {
            ["event"] = signedEvent
        });
    }
}
