using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
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

    private readonly IPeerIdentity peerIdentity;
    private readonly IRooms rooms;
    private readonly IRemoteRooms remoteRooms;
    private readonly IEventSaver eventSaver;

    public InviteController(IPeerIdentity peerIdentity, IRooms rooms, IRemoteRooms remoteRooms, IEventSaver eventSaver)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(eventSaver);

        this.peerIdentity = peerIdentity;
        this.rooms = rooms;
        this.remoteRooms = remoteRooms;
        this.eventSaver = eventSaver;
    }

    [Route("rooms/{roomId}/invite")]
    [HttpPost]
    public async Task<IActionResult> Invite([FromRoute] string roomId, [FromBody] InviteParameters parameters)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var senderId = UserIdentifier.FromId(peerIdentity.Id);
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);

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
            sender: senderId,
            content: JsonSerializer.SerializeToElement(
                new MemberEvent { MemberShip = MembershipStates.Invite },
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        string eventId = EventHash.GetEventId(pdu);

        // Remote invite
        var remoteInviteParameters = new Federation.Protocol.InviteParameters
        {
            Event = pdu
        };
        await remoteRooms.InviteAsync(roomId, eventId, remoteInviteParameters);
        var signedPdu = peerIdentity.SignEvent(pdu);
        await eventSaver.SaveAsync(roomId, eventId, signedPdu, newSnapshot.States);
        return new JsonResult(new object());
    }
}
