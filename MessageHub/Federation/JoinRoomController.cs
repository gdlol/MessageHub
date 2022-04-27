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
public class JoinRoomController : ControllerBase
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IPeerStore peerStore;
    private readonly IRooms rooms;
    private readonly IEventReceiver eventReceiver;
    private readonly IEventPublisher eventPublisher;

    public JoinRoomController(
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

    [Route("make_join/{roomId}/{userId}")]
    [HttpGet]
    public async Task<IActionResult> MakeJoin(string roomId, string userId)
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
                MemberShip = MembershipStates.Join
            }))
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
                new MemberEvent { MemberShip = MembershipStates.Join },
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return new JsonResult(result);
    }



    private async ValueTask<PersistentDataUnit[]> LoadAuthChainAsync(
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
                MemberShip = MembershipStates.Join
            }))
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
        var errors = await eventReceiver.ReceivePersistentEventsAsync(new[] { pdu });
        if (errors.TryGetValue(eventId, out string? error))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.BadState, error));
        }
        await eventPublisher.PublishAsync(pdu);
        var roomEventStore = room.EventStore;
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
            origin = peerIdentity.Id,
            state = statePdus
        });
    }
}
