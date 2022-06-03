using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
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
public class LeaveRoomController : ControllerBase
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly ITimelineLoader timelineLoader;
    private readonly IEventSaver eventSaver;
    private readonly IRemoteRooms remoteRooms;
    private readonly IEventPublisher eventPublisher;

    public LeaveRoomController(
        ILogger<LeaveRoomController> logger,
        IIdentityService identityService,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IEventSaver eventSaver,
        IRemoteRooms remoteRooms,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.logger = logger;
        this.identityService = identityService;
        this.rooms = rooms;
        this.timelineLoader = timelineLoader;
        this.eventSaver = eventSaver;
        this.remoteRooms = remoteRooms;
        this.eventPublisher = eventPublisher;
    }

    [Route("rooms/{roomId}/forget")]
    [HttpPost]
    public async Task<IActionResult> Forget(string roomId)
    {
        logger.LogInformation("Forgetting {}...", roomId);
        var batchStates = await timelineLoader.LoadBatchStatesAsync(id => id == roomId, true);
        if (!batchStates.LeftRoomIds.Contains(roomId))
        {
            logger.LogWarning("Room id not found in left rooms: {}", roomId);
            return BadRequest(MatrixError.Create(MatrixErrorCode.Unknown));
        }
        await eventSaver.ForgetAsync(roomId);
        return new JsonResult(new object());
    }

    private static StrippedStateEvent? TryGetMembershipEvent(
        IReadOnlyDictionary<string, ImmutableList<StrippedStateEvent>> stateEvents,
        string roomId,
        string userId,
        string membership)
    {
        if (!stateEvents.TryGetValue(roomId, out var roomStates))
        {
            return null;
        }
        var result = roomStates.LastOrDefault(x =>
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
            if (memberEvent?.MemberShip != membership)
            {
                return false;
            }
            return true;
        });
        return result;
    }

    [Route("rooms/{roomId}/leave")]
    [HttpPost]
    public async Task<IActionResult> Leave(string roomId)
    {
        var identity = identityService.GetSelfIdentity();
        var sender = UserIdentifier.FromId(identity.Id);
        var userId = sender.ToString();

        logger.LogInformation("Leaving {}...", roomId);
        var batchStates = await timelineLoader.LoadBatchStatesAsync(id => id == roomId, true);
        bool hasInvite = false;
        var inviteOrKnock = TryGetMembershipEvent(batchStates.Invites, roomId, userId, MembershipStates.Invite);
        if (inviteOrKnock is not null)
        {
            hasInvite = true;
            logger.LogDebug("Invite found for {}", roomId);
        }
        else
        {
            inviteOrKnock = TryGetMembershipEvent(batchStates.Knocks, roomId, userId, MembershipStates.Knock);
            if (inviteOrKnock is not null)
            {
                logger.LogDebug("Knock found for {}", roomId);
            }
        }
        if (inviteOrKnock is not null)
        {
            var senderId = UserIdentifier.Parse(inviteOrKnock.Sender);
            string destination = senderId.Id;
            logger.LogInformation("Sending make leave {} to {}...", roomId, destination);
            var pdu = await remoteRooms.MakeLeaveAsync(destination, roomId, userId.ToString());
            if (pdu is null)
            {
                logger.LogWarning("Null make leave response");
                return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(destination)}: {destination}"));
            }

            pdu.Content = JsonSerializer.SerializeToElement(new MemberEvent
            {
                MemberShip = MembershipStates.Leave
            },
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            pdu.Origin = identity.Id;
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

            logger.LogInformation("Sending leave event {} to {}...", roomId, destination);
            await remoteRooms.SendLeaveAsync(destination, roomId, eventId, element);
            if (hasInvite)
            {
                await eventSaver.RejectInviteAsync(roomId);
            }
            else
            {
                await eventSaver.RetractKnockAsync(roomId);
            }
            return new JsonResult(new object());
        }
        else if (batchStates.JoinedRoomIds.Contains(roomId))
        {
            logger.LogDebug("Room id found in joined rooms: {}", roomId);
            var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
            var members = new List<string>();
            foreach (var (roomStateKey, content) in snapshot.StateContents)
            {
                if (roomStateKey.EventType == EventTypes.Member)
                {
                    if (roomStateKey.StateKey == userId)
                    {
                        continue;
                    }
                    var memberEvent = content.Deserialize<MemberEvent>()!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        string member = UserIdentifier.Parse(roomStateKey.StateKey).Id;
                        members.Add(member);
                    }
                }
            }

            var (newSnapshot, pdu) = EventCreation.CreateEvent(
                roomId: roomId,
                snapshot: snapshot,
                eventType: EventTypes.Member,
                stateKey: userId,
                serverKeys: identity.GetServerKeys(),
                sender: sender,
                content: JsonSerializer.SerializeToElement(new MemberEvent
                {
                    MemberShip = MembershipStates.Leave
                },
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            pdu = identity.SignEvent(pdu);
            string eventId = EventHash.GetEventId(pdu);
            if (members.Count > 0)
            {
                // Try to send leave to at least 1 member.
                var tcs = new TaskCompletionSource();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var token = cts.Token;
                using var _ = token.Register(() => tcs.TrySetCanceled());
                var element = pdu.ToJsonElement();
                var __ = Parallel.ForEachAsync(
                    members,
                    new ParallelOptions
                    {
                        CancellationToken = token,
                        MaxDegreeOfParallelism = 3
                    },
                    async (destination, token) =>
                    {
                        try
                        {
                            logger.LogInformation("Sending leave event to {}...", destination);
                            await remoteRooms.SendLeaveAsync(destination, roomId, eventId, element);
                            if (tcs.TrySetResult())
                            {
                                cts.Cancel();
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            logger.LogDebug("Error sending leave event to {}: {}", destination, ex.Message);
                        }
                    });
                try
                {
                    await tcs.Task;
                }
                catch (OperationCanceledException) // timeout
                { }
            }
            await eventSaver.SaveAsync(roomId, eventId, pdu, newSnapshot.States);
            return new JsonResult(new object());
        }
        else
        {
            logger.LogWarning("Room id not found in invite, knock or joined rooms: {}", roomId);
            return BadRequest(MatrixError.Create(MatrixErrorCode.Unknown));
        }
    }

    [Route("rooms/{roomId}/kick")]
    [HttpPost]
    public async Task<IActionResult> Kick([FromRoute] string roomId, [FromBody] KickParameters parameters)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var sender = UserIdentifier.Parse(userId);
        if (!UserIdentifier.TryParse(parameters.UserId, out var target) || target == sender)
        {
            return BadRequest(MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(parameters.UserId)}: {parameters.UserId}"));
        }
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var identity = identityService.GetSelfIdentity();

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        var content = JsonSerializer.SerializeToElement(new MemberEvent
        {
            MemberShip = MembershipStates.Leave,
            Reason = parameters.Reason
        }, ignoreNullOptions);
        if (!authorizer.Authorize(EventTypes.Member, target.ToString(), sender, content))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: EventTypes.Member,
            stateKey: target.ToString(),
            serverKeys: identity.GetServerKeys(),
            sender: sender,
            content: content,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        string eventId = EventHash.GetEventId(pdu);
        var signedPdu = identity.SignEvent(pdu);
        
        await eventPublisher.PublishAsync(signedPdu);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await eventSaver.SaveAsync(roomId, eventId, signedPdu, snapshot.States);
        return new JsonResult(new object());
    }
}
