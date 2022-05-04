using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class JoinRoomController : ControllerBase
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IRemoteRooms remoteRooms;

    public JoinRoomController(IPeerIdentity peerIdentity, IRemoteRooms remoteRooms)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(remoteRooms);

        this.peerIdentity = peerIdentity;
        this.remoteRooms = remoteRooms;
    }

    [Route("join/{roomId}")]
    [Route("rooms/{roomId}/join")]
    [HttpPost]
    public async Task<IActionResult> Join(string roomId)
    {
        var userId = UserIdentifier.FromId(peerIdentity.Id);
        var pdu = await remoteRooms.MakeJoinAsync(roomId, userId.ToString());
        if (pdu is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        pdu.Content = JsonSerializer.SerializeToElement(new MemberEvent
        {
            MemberShip = MembershipStates.Join
        },
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        pdu.Origin = peerIdentity.Id;
        pdu.OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pdu.RoomId = roomId;
        pdu.Sender = userId.ToString();
        pdu.StateKey = userId.ToString();
        pdu.EventType = EventTypes.Member;
        EventHash.UpdateHash(pdu);
        string eventId = EventHash.GetEventId(pdu);
        var element = pdu.ToJsonElement();
        element = peerIdentity.SignJson(element);

        await remoteRooms.SendJoinAsync(roomId, eventId, element);
        _ = remoteRooms.BackfillAsync(roomId);
        return new JsonResult(new { room_id = roomId });
    }
}
