using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServerApi.Sync;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using M = MessageHub.ClientServerProtocol.Events;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomLoader roomLoader;
    private readonly IEventSender eventSender;

    public RoomsController(IRoomLoader roomLoader, IEventSender eventSender)
    {
        ArgumentNullException.ThrowIfNull(roomLoader);
        ArgumentNullException.ThrowIfNull(eventSender);

        this.roomLoader = roomLoader;
        this.eventSender = eventSender;
    }

    [Route("{roomId}/event/{eventId}")]
    [HttpGet]
    public async Task<IActionResult> GetEvent(string roomId, string eventId)
    {
        var clientEvent = await roomLoader.LoadEventAsync(roomId, eventId);
        if (clientEvent is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return new JsonResult(clientEvent, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    [Route("{roomId}/joined_members")]
    [HttpGet]
    public async Task<IActionResult> GetJoinedMembers(string roomId)
    {
        var clientEvents = await roomLoader.LoadRoomMembersAsync(roomId, null);
        if (clientEvents is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return new JsonResult(new
            {
                joined = clientEvents.ToDictionary(
                    clientEvent => clientEvent.StateKey!,
                    clientEvent =>
                    {
                        var memberEvent = JsonSerializer.Deserialize<M.Room.MemberEvent>(clientEvent.Content);
                        return new RoomMember
                        {
                            AvatarUrl = memberEvent?.AvatarUrl,
                            DisplayName = memberEvent?.DisplayName
                        };
                    })
            });
        }
    }

    [Route("{roomId}/members")]
    [HttpGet]
    public async Task<IActionResult> GetMembers(
        [FromRoute] string roomId,
        [FromQuery(Name = "at")] string? at,
        [FromQuery(Name = "membership")] string? membership,
        [FromQuery(Name = "not_membership")] string? notMembership)
    {
        var clientEvents = await roomLoader.LoadRoomMembersAsync(roomId, at);
        if (clientEvents is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            var filter = (ClientEvent clientEvent) =>
            {
                var memberEvent = JsonSerializer.Deserialize<M.Room.MemberEvent>(clientEvent.Content);
                if (membership is null && notMembership is null)
                {
                    return true;
                }
                if (membership is not null && memberEvent?.MemberShip == membership)
                {
                    return true;
                }
                if (notMembership is not null && memberEvent?.MemberShip != notMembership)
                {
                    return true;
                }
                return false;
            };
            return new JsonResult(new
            {
                chunk = clientEvents.Where(filter).ToArray()
            },
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    [Route("{roomId}/state")]
    [HttpGet]
    public async Task<IActionResult> GetStates(string roomId)
    {
        if (!roomLoader.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            var stateEventsWithoutRoomId = await roomLoader.GetRoomStateEvents(roomId, null);
            var stateEvents = stateEventsWithoutRoomId.Select(x => x.ToClientEvent(roomId));
            return new JsonResult(stateEvents, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    [Route("{roomId}/state/{eventType}/{stateKey}")]
    [HttpGet]
    public async Task<IActionResult> GetState(string roomId, string eventType, string stateKey)
    {
        if (!roomLoader.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            var state = await roomLoader.LoadStateAsync(roomId, new RoomStateKey(eventType, stateKey));
            if (state is null)
            {
                return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
            }
            else
            {
                return new JsonResult(state);
            }
        }
    }

    [Route("{roomId}/messages")]
    [HttpGet]
    public async Task<IActionResult> GetMessages(
        [FromRoute] string roomId,
        [FromQuery(Name = "dir"), BindRequired] string direction,
        [FromQuery(Name = "filter")] string? filter,
        [FromQuery(Name = "from"), BindRequired] string from,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "to")] string? to)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.MissingParameter));
        }
        if (!roomLoader.HasRoom(roomId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        if (!new[] { "b", "f" }.Contains(direction))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, $"{nameof(direction)}: {direction}"));
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
            var iterator = await roomLoader.GetTimelineIteratorAsync(roomId, from);
            if (iterator is null)
            {
                return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(from)}: {from}"));
            }
            Func<ValueTask<bool>> move = direction switch
            {
                "b" => iterator.TryMoveBackwardAsync,
                "f" => iterator.TryMoveForwardAsync,
                _ => default!
            };
            var timelineEvents = new List<ClientEventWithoutRoomID>();
            var timelineEventFilter = RoomLoader.GetTimelineEventFilter(roomEventFilter);
            while (true)
            {
                if (iterator.CurrentEvent.EventId == to || timelineEvents.Count >= chunkLimit)
                {
                    break;
                }
                if (timelineEventFilter(iterator.CurrentEvent))
                {
                    timelineEvents.Add(iterator.CurrentEvent);
                }
                if (!await move())
                {
                    break;
                }
            }
            end = iterator.CurrentEvent.EventId;
            chunk = timelineEvents.Select(x => x.ToClientEvent(roomId)).ToArray();
        }
        return new JsonResult(new
        {
            chunk,
            end,
            start = from
        });
    }

    [Route("{roomId}/state/{eventType}/{stateKey}")]
    [HttpPut]
    public async Task<IActionResult> SendStateEvent(
        [FromRoute] string roomId,
        [FromRoute] string eventType,
        [FromRoute] string stateKey,
        [FromBody] JsonElement body)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        if (body.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, $"{nameof(body)}: {body}"));
        }

        var (eventId, error) = await eventSender.SendStateEventAsync(
            userId,
            roomId,
            new RoomStateKey(eventType, stateKey),
            body);
        if (error is not null)
        {
            return BadRequest(error);
        }
        return new JsonResult(new { event_id = eventId });
    }

    [Route("{roomId}/send/{eventType}/{txnId}")]
    [HttpPut]
    public async Task<IActionResult> SendMessageEvent(
        [FromRoute] string roomId,
        [FromRoute] string eventType,
        [FromRoute(Name = "txnId")] string transactionId,
        [FromBody] JsonElement body)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        if (body.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, $"{nameof(body)}: {body}"));
        }

        var (eventId, error) = await eventSender.SendMessageEventAsync(
            userId,
            roomId,
            eventType,
            transactionId,
            body);
        if (error is not null)
        {
            return BadRequest(error);
        }
        return new JsonResult(new { event_id = eventId });
    }
}
