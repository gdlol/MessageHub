using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/federation/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Federation)]
public class StatesController : ControllerBase
{
    private readonly IRooms rooms;

    public StatesController(IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        this.rooms = rooms;
    }

    private static async Task<(PersistentDataUnit[], PersistentDataUnit[])> GetStatesAsync(
        IRoomEventStore roomEventStore,
        string eventId)
    {
        var pdu = await roomEventStore.LoadEventAsync(eventId);
        var authChain = await AuthorizationEventsController.GetAuthChainAsync(roomEventStore, pdu);
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
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        if (!roomSnapshot.StateContents.TryGetValue(
                new RoomStateKey(EventTypes.Member, UserIdentifier.FromId(request.Origin).ToString()),
                out var content)
            || JsonSerializer.Deserialize<MemberEvent>(content) is not MemberEvent memberEvent
            || memberEvent.MemberShip != MembershipStates.Join)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        if (eventId is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(eventId)));
        }

        var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var (authChain, statePdus) = await GetStatesAsync(roomEventStore, eventId);
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
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        if (!roomSnapshot.StateContents.TryGetValue(
                new RoomStateKey(EventTypes.Member, UserIdentifier.FromId(request.Origin).ToString()),
                out var content)
            || JsonSerializer.Deserialize<MemberEvent>(content) is not MemberEvent memberEvent
            || memberEvent.MemberShip != MembershipStates.Join)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        if (eventId is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(eventId)));
        }

        var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(new[] { eventId });
        if (missingEventIds.Length > 0)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(eventId)));
        }
        var (authChain, statePdus) = await GetStatesAsync(roomEventStore, eventId);
        return new JsonResult(new
        {
            auth_chain_ids = authChain.Select(x => EventHash.GetEventId(x)).ToArray(),
            pdu_ids = statePdus.Select(x => EventHash.GetEventId(x)).ToArray()
        });
    }
}
