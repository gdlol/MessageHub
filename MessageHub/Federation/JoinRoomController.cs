using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class JoinRoomController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;
    private readonly IEventReceiver eventReceiver;
    private readonly IEventPublisher eventPublisher;

    public JoinRoomController(
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

    [Route("make_join/{roomId}/{userId}")]
    [HttpGet]
    public async Task<IActionResult> MakeJoin(string roomId, string userId)
    {
        var identity = identityService.GetSelfIdentity();
        var request = HttpContext.GetSignedRequest();
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
        var joinContent = DefaultJsonSerializer.SerializeToElement(
            new MemberEvent { MemberShip = MembershipStates.Join });
        if (!eventAuthorizer.Authorize(
            eventType: EventTypes.Member,
            stateKey: userId,
            sender: senderId,
            content: joinContent))
        {
            if (eventAuthorizer.TryGetJoinRulesEvent()?.JoinRule == JoinRules.Public)
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
            content: joinContent,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return new JsonResult(pdu.ToJsonElement());
    }

    private static async ValueTask<PersistentDataUnit[]> LoadAuthChainAsync(
        IRoomEventStore roomEventStore,
        IEnumerable<PersistentDataUnit> states)
    {
        var authEventIds = new HashSet<string>();
        foreach (var stateEvent in states)
        {
            foreach (string authEventId in stateEvent.AuthorizationEvents)
            {
                authEventIds.Add(authEventId);
            }
        }
        var authChainIds = new HashSet<string>(authEventIds);
        var authChain = new List<PersistentDataUnit>();
        while (authEventIds.Count > 0)
        {
            var newAuthEventIds = new HashSet<string>();
            foreach (string authEventId in authEventIds)
            {
                var pdu = await roomEventStore.LoadEventAsync(authEventId);
                authChain.Add(pdu);
                foreach (string newAuthEventId in pdu.AuthorizationEvents)
                {
                    if (authChainIds.Add(newAuthEventId))
                    {
                        newAuthEventIds.Add(newAuthEventId);
                    }
                }
            }
            authEventIds = newAuthEventIds;
        }
        return authChain.ToArray();
    }

    [Route("send_join/{roomId}/{eventId}")]
    [HttpPut]
    public async Task<IActionResult> SendJoin(
        [FromRoute] string roomId,
        [FromRoute] string eventId,
        [FromBody] PersistentDataUnit pdu)
    {
        var identity = identityService.GetSelfIdentity();
        var request = HttpContext.GetSignedRequest();
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
            if (memberEvent?.MemberShip != MembershipStates.Join)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter));
            }
        }
        catch (Exception)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var eventAuthorizer = new EventAuthorizer(roomSnapshot.StateContents);
        if (!eventAuthorizer.Authorize(pdu.EventType, pdu.StateKey, sender, pdu.Content))
        {
            if (eventAuthorizer.TryGetJoinRulesEvent()?.JoinRule == JoinRules.Public)
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
        if (roomSnapshot.States.TryGetValue(new RoomStateKey(pdu.EventType, pdu.StateKey), out string? oldEventId))
        {
            using var store = await rooms.GetRoomEventStoreAsync(roomId);
            var oldEvent = await store.LoadEventAsync(oldEventId);
            var unsigned = new Dictionary<string, object>();
            if (pdu.Unsigned is not null)
            {
                try
                {
                    unsigned = pdu.Unsigned.Value.Deserialize<Dictionary<string, object>>() ?? unsigned;
                }
                catch (Exception)
                { }
            }
            unsigned["replaces_state"] = oldEventId;
            unsigned["prev_content"] = oldEvent.Content;
            unsigned["prev_sender"] = oldEvent.Sender;
            pdu.Unsigned = DefaultJsonSerializer.SerializeToElement(unsigned);
        }
        var errors = await eventReceiver.ReceivePersistentEventsAsync(new[] { pdu });
        if (errors.TryGetValue(eventId, out string? error) && !string.IsNullOrEmpty(error))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.BadState, error));
        }
        await eventPublisher.PublishAsync(pdu);
        var roomStateResolver = new RoomStateResolver(roomEventStore);
        var states = await roomStateResolver.ResolveStateAsync(pdu.PreviousEvents);
        var statePdus = new List<PersistentDataUnit>();
        foreach (string stateEventId in states.Values)
        {
            var statePdu = await roomEventStore.LoadEventAsync(stateEventId);
            statePdus.Add(statePdu);
        }
        var authChain = await LoadAuthChainAsync(roomEventStore, statePdus);
        return new JsonResult(new
        {
            auth_chain = authChain,
            origin = identity.Id,
            state = statePdus
        });
    }
}
