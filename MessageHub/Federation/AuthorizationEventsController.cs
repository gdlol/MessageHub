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
public class AuthorizationEventsController : ControllerBase
{
    private readonly IRooms rooms;

    public AuthorizationEventsController(IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        this.rooms = rooms;
    }

    public static async Task<PersistentDataUnit[]> GetAuthChainAsync(IRoom room, PersistentDataUnit pdu)
    {
        var roomEventStore = room.EventStore;
        var authEventIds = pdu.AuthorizationEvents.ToList();
        var eventMap = new Dictionary<string, PersistentDataUnit>();
        var eventIds = new List<string>();
        while (authEventIds.Count > 0)
        {
            var newAuthEventIds = new HashSet<string>();
            foreach (string authEventId in authEventIds)
            {
                pdu = await roomEventStore.LoadEventAsync(authEventId);
                eventMap[authEventId] = pdu;
                eventIds.Add(authEventId);
                newAuthEventIds.UnionWith(pdu.AuthorizationEvents);
            }
            authEventIds = newAuthEventIds.Except(eventMap.Keys).ToList();
        }
        return eventIds.Select(x => eventMap[x]).ToArray();
    }

    [Route("event_auth/{roomId}/{eventId}")]
    [HttpGet]
    public async Task<IActionResult> GetAuthorizationEvents(string roomId, string eventId)
    {
        SignedRequest request = (SignedRequest)Request.HttpContext.Items[nameof(request)]!;
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var room = await rooms.GetRoomAsync(roomId);
        if (!room.Members.TryGetValue(UserIdentifier.FromId(request.Origin).ToString(), out var memberEvent)
            || memberEvent.MemberShip != MembershipStates.Join)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }

        var roomEventStore = room.EventStore;
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var pdu = await roomEventStore.LoadEventAsync(eventId);
        var authChain = await GetAuthChainAsync(room, pdu);
        return new JsonResult(new
        {
            auth_chain = authChain
        });
    }
}
