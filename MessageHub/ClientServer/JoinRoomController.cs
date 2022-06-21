using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class JoinRoomController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserProfile userProfile;
    private readonly IRemoteRooms remoteRooms;
    private readonly ITimelineLoader timelineLoader;

    public JoinRoomController(
        ILogger<JoinRoomController> logger,
        IIdentityService identityService,
        IUserProfile userProfile,
        IRemoteRooms remoteRooms,
        ITimelineLoader timelineLoader)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);

        this.logger = logger;
        this.identityService = identityService;
        this.userProfile = userProfile;
        this.remoteRooms = remoteRooms;
        this.timelineLoader = timelineLoader;
    }

    [Route("join/{roomId}")]
    [Route("rooms/{roomId}/join")]
    [HttpPost]
    public async Task<IActionResult> Join(string roomId)
    {
        var identity = identityService.GetSelfIdentity();
        var userId = UserIdentifier.FromId(identity.Id);

        logger.LogInformation("Joining {}...", roomId);
        var batchStates = await timelineLoader.LoadBatchStatesAsync(id => id == roomId, true);
        if (!batchStates.Invites.TryGetValue(roomId, out var roomStates))
        {
            logger.LogWarning("Invite not found for {}", roomId);
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var inviteEvent = roomStates.LastOrDefault(x =>
        {
            if (x.EventType != EventTypes.Member)
            {
                return false;
            }
            if (x.StateKey != userId.ToString())
            {
                return false;
            }
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(x.Content);
            if (memberEvent?.MemberShip != MembershipStates.Invite)
            {
                return false;
            }
            return true;
        });
        if (inviteEvent is null)
        {
            logger.LogWarning("Invite not found for {}", roomId);
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        string destination = UserIdentifier.Parse(inviteEvent.Sender).Id;
        logger.LogInformation("Sending make join {} to {}...", roomId, destination);
        var pdu = await remoteRooms.MakeJoinAsync(destination, roomId, userId.ToString());
        if (pdu is null)
        {
            logger.LogWarning("Null make join response");
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId.ToString());
        string? displayName = await userProfile.GetDisplayNameAsync(userId.ToString());
        pdu.Content = JsonSerializer.SerializeToElement(new MemberEvent
        {
            AvatarUrl = avatarUrl,
            DisplayName = displayName,
            MemberShip = MembershipStates.Join
        },
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        pdu.OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pdu.RoomId = roomId;
        pdu.Sender = userId.ToString();
        pdu.StateKey = userId.ToString();
        pdu.EventType = EventTypes.Member;
        pdu.ServerKeys = identity.GetServerKeys();
        EventHash.UpdateHash(pdu);
        string eventId = EventHash.GetEventId(pdu);
        pdu = identity.SignEvent(pdu);
        var element = pdu.ToJsonElement();

        logger.LogInformation("Sending send join {} to {}...", roomId, destination);
        await remoteRooms.SendJoinAsync(destination, roomId, eventId, element);
        logger.LogInformation("Backfill {} from {}...", roomId, destination);
        _ = remoteRooms.BackfillAsync(destination, roomId);
        return new JsonResult(new { room_id = roomId });
    }
}
