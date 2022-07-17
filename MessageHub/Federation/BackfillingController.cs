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
public class BackfillingController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;

    public BackfillingController(IIdentityService identityService, IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);

        this.identityService = identityService;
        this.rooms = rooms;
    }

    [Route("backfill/{roomId}")]
    [HttpGet]
    public async Task<IActionResult> Backfill(
        [FromRoute] string roomId,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "v")] string[]? eventIds)
    {
        var request = Request.HttpContext.GetSignedRequest();
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
        if (limit is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, "limit"));
        }
        if (eventIds is null || eventIds.Length == 0)
        {
            eventIds = roomSnapshot.LatestEventIds.ToArray();
        }
        else if (eventIds.Any(x => x is null))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, "v"));
        }
        eventIds = eventIds.Distinct().Take(20).ToArray();
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        limit = Math.Min(limit.Value, 100);
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(eventIds);
        var latestEventIds = eventIds.Except(missingEventIds).ToList();
        var eventMap = new Dictionary<string, PersistentDataUnit>();
        var foundEventIds = new List<string>();
        while (latestEventIds.Count > 0 && foundEventIds.Count < limit.Value)
        {
            var newLatestEventIds = new HashSet<string>();
            foreach (string eventId in latestEventIds)
            {
                if (foundEventIds.Count >= limit.Value)
                {
                    break;
                }
                var pdu = await roomEventStore.LoadEventAsync(eventId);
                eventMap[eventId] = pdu;
                foundEventIds.Add(eventId);
                newLatestEventIds.UnionWith(pdu.PreviousEvents);
            }
            latestEventIds = newLatestEventIds.Except(eventMap.Keys).ToList();
        }
        var pdus = foundEventIds.Select(x => eventMap[x].ToJsonElement()).ToArray();
        return new JsonResult(new
        {
            origin = identityService.GetSelfIdentity().Id,
            origin_server_ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pdus
        });
    }

    [Route("get_missing_events/{roomId}")]
    [HttpPost]
    public async Task<IActionResult> GetMissingEvents(
        [FromRoute] string roomId,
        [FromBody] GetMissingEventsRequest request)
    {
        var signedRequest = Request.HttpContext.GetSignedRequest();
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        var roomSnapshot = await rooms.GetRoomSnapshotAsync(roomId);
        if (!roomSnapshot.StateContents.TryGetValue(
                new RoomStateKey(EventTypes.Member, UserIdentifier.FromId(signedRequest.Origin).ToString()),
                out var content)
            || JsonSerializer.Deserialize<MemberEvent>(content) is not MemberEvent memberEvent
            || memberEvent.MemberShip != MembershipStates.Join)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, nameof(roomId)));
        }
        if (request.MinDepth > 3)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(request.MinDepth)));
        }
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        int limit = Math.Min(request.Limit, 100);
        var earliestEvents = request.EarliestEvents.Where(x => x is not null).ToHashSet();
        var eventMap = new Dictionary<string, PersistentDataUnit>();
        var foundEventIds = new List<string>();
        var latestEventIds = request.LatestEvents.Where(x => x is not null).Distinct().ToList();
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(latestEventIds.ToArray());
        latestEventIds = latestEventIds.Except(missingEventIds).ToList();
        int depth = -1;
        while (latestEventIds.Count > 0 && foundEventIds.Count < limit)
        {
            var newLatestEventIds = new HashSet<string>();
            foreach (string eventId in latestEventIds)
            {
                if (foundEventIds.Count >= limit)
                {
                    break;
                }
                var pdu = await roomEventStore.LoadEventAsync(eventId);
                if (depth >= request.MinDepth && !earliestEvents.Contains(eventId))
                {
                    eventMap[eventId] = pdu;
                    foundEventIds.Add(eventId);
                }
                newLatestEventIds.UnionWith(pdu.PreviousEvents);
            }
            latestEventIds = newLatestEventIds
                .Except(eventMap.Keys)
                .Except(earliestEvents)
                .ToList();
            depth += 1;
        }
        return new JsonResult(new
        {
            events = foundEventIds.Select(x => eventMap[x].ToJsonElement()).ToArray()
        });
    }
}
