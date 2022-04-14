using System.Text.Json;
using MessageHub.ClientServerProtocol;
using MessageHub.ClientServerProtocol.Events.Room;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class ListRoomsController : ControllerBase
{
    private readonly IHostInfo hostInfo;
    private readonly IRoomLoader roomLoader;
    private readonly IPersistenceService persistenceService;

    public ListRoomsController(
        IHostInfo hostInfo,
        IRoomLoader roomLoader,
        IPersistenceService persistenceService)
    {
        ArgumentNullException.ThrowIfNull(hostInfo);
        ArgumentNullException.ThrowIfNull(roomLoader);
        ArgumentNullException.ThrowIfNull(persistenceService);

        this.hostInfo = hostInfo;
        this.roomLoader = roomLoader;
        this.persistenceService = persistenceService;
    }

    [Route("directory/list/room/{roomId}")]
    [HttpGet]
    public async Task<IActionResult> GetVisibility(string roomId)
    {
        string? visibility = await persistenceService.GetRoomVisibilityAsync(roomId);
        if (visibility is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        return new JsonResult(new { visibility });
    }

    [Route("directory/list/room/{roomId}")]
    [HttpPut]
    public async Task<IActionResult> SetVisibility(
        [FromRoute] string roomId,
        [FromBody] SetVisibilityParameters parameters)
    {
        bool result = await persistenceService.SetRoomVisibilityAsync(roomId, parameters.Visibility);
        if (result is false)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return new JsonResult(new object());
        }
    }

    private static PublicRoomsChunk GetPublicRoomsChunk(string roomId, ClientEventWithoutRoomID[] stateEvents)
    {
        var chunk = new PublicRoomsChunk
        {
            RoomId = roomId
        };
        int memberCount = 0;
        foreach (var stateEvent in stateEvents)
        {
            if (stateEvent.EventType == EventTypes.Avatar)
            {
                var content = JsonSerializer.Deserialize<AvatarEvent>(stateEvent.Content)!;
                chunk.AvatarUrl = content.Url;
            }
            else if (stateEvent.EventType == EventTypes.CanonicalAlias)
            {
                var content = JsonSerializer.Deserialize<CanonicalAliasEvent>(stateEvent.Content)!;
                chunk.CanonicalAlias = content.Alias;
            }
            else if (stateEvent.EventType == EventTypes.JoinRules)
            {
                var content = JsonSerializer.Deserialize<JoinRulesEvent>(stateEvent.Content)!;
                chunk.JoinRule = content.JoinRule;
            }
            else if (stateEvent.EventType == EventTypes.Name)
            {
                var content = JsonSerializer.Deserialize<NameEvent>(stateEvent.Content)!;
                chunk.Name = content.Name;
            }
            else if (stateEvent.EventType == EventTypes.Member)
            {
                var content = JsonSerializer.Deserialize<MemberEvent>(stateEvent.Content)!;
                if (content.MemberShip == MembershipStates.Join)
                {
                    memberCount += 1;
                }
            }
            else if (stateEvent.EventType == EventTypes.Topic)
            {
                var content = JsonSerializer.Deserialize<TopicEvent>(stateEvent.Content)!;
                chunk.Topic = content.Topic;
            }
            else if (stateEvent.EventType == EventTypes.HistoryVisibility)
            {
                var content = JsonSerializer.Deserialize<HistoryVisibilityEvent>(stateEvent.Content)!;
                chunk.WorldReadable = content.HistoryVisibility == HistoryVisibilityKinds.WorldReadable;
            }
        }
        return chunk;
    }

    private static IEnumerable<PublicRoomsChunk> FilterChunks(
        IEnumerable<PublicRoomsChunk> chunks,
        JsonElement? filter)
    {
        if (filter is null || filter.Value.ValueKind != JsonValueKind.Object)
        {
            return chunks;
        }
        if (!filter.Value.TryGetProperty("generic_search_term", out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return chunks;
        }
        string? searchTerm = value.GetString();
        if (string.IsNullOrEmpty(searchTerm))
        {
            return chunks;
        }
        var tokens = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<PublicRoomsChunk> filterFunc()
        {
            foreach (var chunk in chunks)
            {
                foreach (string token in tokens)
                {
                    if (chunk.Name?.Contains(token, StringComparison.InvariantCultureIgnoreCase) == true)
                    {
                        yield return chunk;
                        break;
                    }
                    if (chunk.Topic?.Contains(token, StringComparison.InvariantCultureIgnoreCase) == true)
                    {
                        yield return chunk;
                        break;
                    }
                    if (chunk.CanonicalAlias?.Contains(token, StringComparison.InvariantCultureIgnoreCase) == true)
                    {
                        yield return chunk;
                        break;
                    }
                }
            }
        };
        return filterFunc();
    }

    [Route("publicRooms")]
    [HttpPost]
    public async Task<IActionResult> GetPublicRooms(
        [FromQuery] string? server,
        [FromBody] GetPublicRoomsParameters parameters)
    {
        GetPublicRoomsResponse result;
        if (server is not null && server != hostInfo.ServerName)
        {
            result = new GetPublicRoomsResponse
            {
                Chunk = Array.Empty<PublicRoomsChunk>()
            };
        }
        else if (parameters.ThirdPartyInstanceId is not null)
        {
            result = new GetPublicRoomsResponse
            {
                Chunk = Array.Empty<PublicRoomsChunk>()
            };
        }
        else
        {
            var publicRoomIds = await persistenceService.GetPublicRoomListAsync();
            if (parameters.Since is string since)
            {
                publicRoomIds = publicRoomIds.SkipWhile(x => x != since).ToArray();
            }
            var chunks = new List<PublicRoomsChunk>();
            foreach (string roomId in publicRoomIds)
            {
                var stateEvents = await roomLoader.GetRoomStateEvents(roomId, null);
                var chunk = GetPublicRoomsChunk(roomId, stateEvents);
                chunks.Add(chunk);
            }
            var filteredChunks = FilterChunks(chunks, parameters.Filter).ToArray();
            string? nextBatch = null;
            if (parameters.Limit is int limit && limit > filteredChunks.Length)
            {
                nextBatch = filteredChunks[limit].RoomId;
            }
            int roomCount = filteredChunks.Length;
            result = new GetPublicRoomsResponse
            {
                Chunk = filteredChunks,
                NextBatch = nextBatch,
                TotalRoomCountEstimate = roomCount
            };
        }
        return new JsonResult(result);
    }

    [Route("publicRooms")]
    [HttpGet]
    public Task<IActionResult> GetPublicRooms(
        [FromQuery] int? limit,
        [FromQuery] string? server,
        [FromQuery] string? since)
    {
        return GetPublicRooms(server, new GetPublicRoomsParameters
        {
            Limit = limit,
            Since = since
        });
    }
}
