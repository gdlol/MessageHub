using System.Text.Json;
using MessageHub.ClientServer.Sync;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Events;
using Microsoft.AspNetCore.Authorization;
using MessageHub.Authentication;
using MessageHub.HomeServer.Events.Room;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/rooms")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class GetMessagesController : ControllerBase
{
    private readonly ILogger logger;
    private readonly ITimelineLoader timelineLoader;
    private readonly IRooms rooms;

    public GetMessagesController(ILogger<GetMessagesController> logger, ITimelineLoader timelineLoader, IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);

        this.logger = logger;
        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
    }

    [Route("{roomId}/messages")]
    [HttpGet]
    public async Task<IActionResult> GetMessages(
        [FromRoute] string roomId,
        [FromQuery(Name = "dir")] string? direction,
        [FromQuery(Name = "filter")] string? filter,
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "to")] string? to)
    {
        if (direction is null)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(direction)));
        }
        var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, true);
        if (!batchStates.JoinedRoomIds.Contains(roomId) && !batchStates.LeftRoomIds.Contains(roomId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        if (!new[] { "b", "f" }.Contains(direction))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, $"{nameof(direction)}: {direction}"));
        }
        if (from is null)
        {
            if (!batchStates.RoomEventIds.TryGetValue(roomId, out string? eventId))
            {
                return new JsonResult(new
                {
                    chunk = Array.Empty<object>()
                });
            }
            if (direction == "b")
            {
                from = eventId;
            }
            else
            {
                using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                var states = await roomEventStore.LoadStatesAsync(eventId);
                if (!states.TryGetValue(new RoomStateKey(EventTypes.Create, string.Empty), out eventId))
                {
                    logger.LogWarning("Create event for room {} not found.", roomId);
                    return new JsonResult(new
                    {
                        chunk = Array.Empty<object>()
                    });
                }
                from = eventId;
            }
        }
        int chunkLimit = limit ?? 10;
        RoomEventFilter? roomEventFilter = null;
        if (filter is not null)
        {
            try
            {
                var element = JsonSerializer.Deserialize<JsonElement>(filter);
                try
                {
                    roomEventFilter = element.Deserialize<RoomEventFilter>();
                }
                catch (Exception)
                {
                    return BadRequest(MatrixError.Create(MatrixErrorCode.BadJson, nameof(filter)));
                }
            }
            catch (Exception)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.NotJson, nameof(filter)));
            }
        }
        ClientEvent[]? chunk = null;
        string? end = null;
        if (roomEventFilter is not null)
        {
            if (roomEventFilter.Rooms is not null && !roomEventFilter.Rooms.Contains(roomId))
            {
                chunk = Array.Empty<ClientEvent>();
            }
            if (roomEventFilter.NotRooms is not null && roomEventFilter.NotRooms.Contains(roomId))
            {
                chunk = Array.Empty<ClientEvent>();
            }
            if (roomEventFilter.Limit is not null)
            {
                chunkLimit = Math.Min(roomEventFilter.Limit.Value, chunkLimit);
            }
        }
        if (chunk is null)
        {
            using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
            var iterator = await timelineLoader.GetTimelineIteratorAsync(roomId, from);
            if (iterator is null)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(from)}: {from}"));
            }
            using var _ = iterator;
            Func<ValueTask<bool>> tryMove = direction switch
            {
                "b" => iterator.TryMoveBackwardAsync,
                "f" => iterator.TryMoveForwardAsync,
                _ => default!
            };
            var timelineEvents = new List<PersistentDataUnit>();
            var timelineEventFilter = RoomsLoader.GetTimelineEventFilter(roomEventFilter);
            while (true)
            {
                if (iterator.CurrentEventId == to || timelineEvents.Count >= chunkLimit)
                {
                    break;
                }
                var currentEvent = await roomEventStore.LoadEventAsync(iterator.CurrentEventId);
                if (timelineEventFilter(currentEvent))
                {
                    timelineEvents.Add(currentEvent);
                }
                if (!await tryMove())
                {
                    break;
                }
            }
            end = iterator.CurrentEventId;
            chunk = timelineEvents.Select(ClientEvent.FromPersistentDataUnit).ToArray();
        }
        return new JsonResult(new
        {
            chunk,
            end,
            start = from
        });
    }
}
