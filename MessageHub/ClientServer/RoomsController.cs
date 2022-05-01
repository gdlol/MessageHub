using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Sync;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using MessageHub.HomeServer.Rooms.Timeline;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Events;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/rooms")]
public class RoomsController : ControllerBase
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPeerIdentity peerIdentity;
    private readonly IRooms rooms;
    private readonly ITimelineLoader timelineLoader;
    private readonly IEventSaver eventSaver;

    public RoomsController(
        IPeerIdentity peerIdentity,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IEventSaver eventSaver)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(eventSaver);

        this.peerIdentity = peerIdentity;
        this.rooms = rooms;
        this.timelineLoader = timelineLoader;
        this.eventSaver = eventSaver;
    }

    [Route("{roomId}/event/{eventId}")]
    [HttpGet]
    public async Task<IActionResult> GetEvent(string roomId, string eventId)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var pdu = await roomEventStore.TryLoadEventAsync(eventId);
        if (pdu is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(eventId)}: {eventId}"));
        }
        else
        {
            return new JsonResult(ClientEvent.FromPersistentDataUnit(pdu), ignoreNullOptions);
        }
    }

    [Route("{roomId}/joined_members")]
    [HttpGet]
    public async Task<IActionResult> GetJoinedMembers(string roomId)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var joined = new Dictionary<string, RoomMember>();
        foreach (var (roomStateKey, content) in snapshot.StateContents)
        {
            if (roomStateKey.EventType == EventTypes.Member)
            {
                var memberEvent = content.Deserialize<MemberEvent>()!;
                if (memberEvent.MemberShip == MembershipStates.Join)
                {
                    joined[roomStateKey.StateKey] = new RoomMember
                    {
                        AvatarUrl = memberEvent.AvatarUrl,
                        DisplayName = memberEvent.DisplayName
                    };
                }
            }
        }
        return new JsonResult(new { joined }, ignoreNullOptions);
    }

    [Route("{roomId}/members")]
    [HttpGet]
    public async Task<IActionResult> GetMembers(
        [FromRoute] string roomId,
        [FromQuery(Name = "at")] string? at,
        [FromQuery(Name = "membership")] string? membership,
        [FromQuery(Name = "not_membership")] string? notMembership)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        RoomSnapshot snapshot;
        if (at is null)
        {
            snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        }
        else
        {
            snapshot = await roomEventStore.LoadSnapshotAsync(at);
        }

        var clientEvents = new List<ClientEvent>();
        var filter = (ClientEvent clientEvent) =>
        {
            if (membership is null && notMembership is null)
            {
                return true;
            }
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(clientEvent.Content)!;
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
        foreach (var (roomStateKey, eventId) in snapshot.States)
        {
            if (roomStateKey.EventType == EventTypes.Member)
            {
                var pdu = await roomEventStore.LoadEventAsync(eventId);
                var clientEvent = ClientEvent.FromPersistentDataUnit(pdu);
                if (filter(clientEvent))
                {
                    clientEvents.Add(clientEvent);
                }
            }
        }
        return new JsonResult(new { chunk = clientEvents }, ignoreNullOptions);
    }

    [Route("{roomId}/state")]
    [HttpGet]
    public async Task<IActionResult> GetStates(string roomId)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
        var stateEvents = new List<ClientEvent>();
        foreach (var eventId in snapshot.States.Values)
        {
            var pdu = await roomEventStore.LoadEventAsync(eventId);
            var clientEvent = ClientEvent.FromPersistentDataUnit(pdu);
            stateEvents.Add(clientEvent);
        }
        return new JsonResult(stateEvents, ignoreNullOptions);
    }

    [Route("{roomId}/state/{eventType}/{stateKey?}")]
    [HttpGet]
    public async Task<IActionResult> GetState(string roomId, string eventType, string? stateKey)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        if (snapshot.StateContents.TryGetValue(new RoomStateKey(eventType, stateKey ?? string.Empty), out var content))
        {
            return new JsonResult(content);
        }
        else
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
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
        if (!timelineLoader.HasRoom(roomId))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        if (from is null)
        {
            var eventIds = await timelineLoader.GetRoomEventIds(null);
            if (!eventIds.TryGetValue(roomId, out string? eventId))
            {
                return new JsonResult(new
                {
                    chunk = Array.Empty<object>()
                });
            }
            from = eventId;
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
            var iterator = await timelineLoader.GetTimelineIteratorAsync(roomId, from);
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
                string eventId = EventHash.GetEventId(iterator.CurrentEvent);
                if (eventId == to || timelineEvents.Count >= chunkLimit)
                {
                    break;
                }
                var clientEvent = ClientEventWithoutRoomID.FromPersistentDataUnit(iterator.CurrentEvent);
                if (timelineEventFilter(clientEvent))
                {
                    timelineEvents.Add(clientEvent);
                }
                if (!await move())
                {
                    break;
                }
            }
            end = EventHash.GetEventId(iterator.CurrentEvent);
            chunk = timelineEvents.Select(x => x.ToClientEvent(roomId)).ToArray();
        }
        return new JsonResult(new
        {
            chunk,
            end,
            start = from
        });
    }

    [Route("{roomId}/state/{eventType}/{stateKey?}")]
    [HttpPut]
    public async Task<IActionResult> SendStateEvent(
        [FromRoute] string roomId,
        [FromRoute] string eventType,
        [FromRoute] string? stateKey,
        [FromBody] JsonElement body)
    {
        stateKey ??= string.Empty;
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var senderId = UserIdentifier.Parse(userId);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(eventType, stateKey, senderId, body))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: eventType,
            stateKey: stateKey,
            sender: senderId,
            content: body,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        string eventId = EventHash.GetEventId(pdu);
        var element = pdu.ToJsonElement();
        element = peerIdentity.SignJson(element);
        await eventSaver.SaveAsync(roomId, eventId, element, snapshot.States);
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
        var senderId = UserIdentifier.Parse(userId);
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }

        var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
        var authorizer = new EventAuthorizer(snapshot.StateContents);
        if (!authorizer.Authorize(eventType, null, senderId, body))
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        (snapshot, var pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: snapshot,
            eventType: eventType,
            stateKey: null,
            sender: senderId,
            content: body,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            unsigned: JsonSerializer.SerializeToElement(
                new UnsignedData
                {
                    TransactionId = transactionId
                },
                ignoreNullOptions));
        string eventId = EventHash.GetEventId(pdu);
        var element = pdu.ToJsonElement();
        element = peerIdentity.SignJson(element);
        await eventSaver.SaveAsync(roomId, eventId, element, snapshot.States);
        return new JsonResult(new { event_id = eventId });
    }
}
