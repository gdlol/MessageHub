using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/key/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class StatesController : ControllerBase
{
    private readonly IRooms rooms;

    public StatesController(IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        this.rooms = rooms;
    }

    private async Task<(PersistentDataUnit[], PersistentDataUnit[])> GetStatesAsync(IRoom room, string eventId)
    {
        var roomEventStore = room.EventStore;
        var pdu = await roomEventStore.LoadEventAsync(eventId);
        var authChain = await AuthorizationEventsController.GetAuthChainAsync(room, pdu);
        var states = await roomEventStore.LoadStatesAsync(eventId);
        var statePdus = new List<PersistentDataUnit>();
        foreach (string stateEventId in states.Values)
        {
            pdu = await roomEventStore.LoadEventAsync(stateEventId);
            statePdus.Add(pdu);
        }
        return (authChain, statePdus.ToArray());
    }

    [Route("state/{roomId}")]
    [HttpPost]
    public async Task<IActionResult> GetStates(
        [FromRoute] string roomId,
        [FromQuery(Name = "event_id")] string? eventId)
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
        if (eventId is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(eventId)));
        }

        var roomEventStore = room.EventStore;
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var (authChain, statePdus) = await GetStatesAsync(room, eventId);
        return new JsonResult(new
        {
            auth_chain = authChain,
            pdus = statePdus
        });
    }


    [Route("state_ids/{roomId}")]
    [HttpPost]
    public async Task<IActionResult> GetStateIds(
        [FromRoute] string roomId,
        [FromQuery(Name = "event_id")] string? eventId)
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
        if (eventId is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(eventId)));
        }

        var roomEventStore = room.EventStore;
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var (authChain, statePdus) = await GetStatesAsync(room, eventId);
        return new JsonResult(new
        {
            auth_chain_ids = authChain.Select(x => EventHash.GetEventId(x)).ToArray(),
            pdu_ids = statePdus.Select(x => EventHash.GetEventId(x)).ToArray()
        });
    }
}
