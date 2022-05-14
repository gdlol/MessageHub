using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Events.Room;
using Microsoft.AspNetCore.Authorization;
using MessageHub.Authentication;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/rooms")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class RoomsController : ControllerBase
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IRooms rooms;

    public RoomsController(IRooms rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        this.rooms = rooms;
    }

    [Route("{roomId}/event/{eventId}")]
    [HttpGet]
    public async Task<IActionResult> GetEvent(string roomId, string eventId)
    {
        if (!rooms.HasRoom(roomId))
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound, $"{nameof(roomId)}: {roomId}"));
        }
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
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
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
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
        using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
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
}
