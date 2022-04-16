using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class SearchUserController : ControllerBase
{
    public class SearchRequestBody
    {
        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [Required]
        [JsonPropertyName("search_term")]
        public string SearchTerm { get; set; } = default!;
    }

    public class SearchResponse
    {
        public class User
        {
            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }

            [Required]
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }
        }

        [Required]
        [JsonPropertyName("limited")]
        public bool Limited { get; set; }

        [Required]
        [JsonPropertyName("results")]
        public User[] Results { get; set; } = default!;
    }

    private readonly IRoomLoader roomLoader;

    public SearchUserController(IRoomLoader roomLoader)
    {
        ArgumentNullException.ThrowIfNull(roomLoader);

        this.roomLoader = roomLoader;
    }

    [Route("user_directory/search")]
    [HttpPost]
    public async Task<IActionResult> SearchUser([FromBody] SearchRequestBody requestBody)
    {
        if (string.IsNullOrEmpty(requestBody.SearchTerm))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(requestBody.SearchTerm)));
        }

        var avatarUrls = new ConcurrentDictionary<string, (string url, long timestamp)>();
        var displayNames = new ConcurrentDictionary<string, (string name, long timestamp)>();
        var userIds = new HashSet<string>();
        void updateUserInfo(ClientEventWithoutRoomID stateEvent)
        {
            if (stateEvent.EventType != EventTypes.Member || stateEvent.StateKey is null)
            {
                return;
            }
            var memberEvent = JsonSerializer.Deserialize<MemberEvent>(stateEvent.Content)!;
            string userId = stateEvent.StateKey;
            userIds.Add(userId);
            if (memberEvent.AvatarUrl is not null)
            {
                avatarUrls.AddOrUpdate(
                    userId,
                    (memberEvent.AvatarUrl, stateEvent.OriginServerTimestamp),
                    (key, value) =>
                    {
                        if (stateEvent.OriginServerTimestamp > value.timestamp)
                        {
                            return (memberEvent.AvatarUrl, stateEvent.OriginServerTimestamp);
                        }
                        else
                        {
                            return value;
                        }
                    });
            }
            if (memberEvent.DisplayName is not null)
            {
                displayNames.AddOrUpdate(
                    userId,
                    (memberEvent.DisplayName, stateEvent.OriginServerTimestamp),
                    (key, value) =>
                    {
                        if (stateEvent.OriginServerTimestamp > value.timestamp)
                        {
                            return (memberEvent.DisplayName, stateEvent.OriginServerTimestamp);
                        }
                        else
                        {
                            return value;
                        }
                    });
            }
        }
        var roomStates = await roomLoader.LoadRoomStatesAsync(_ => true, includeLeave: false);
        foreach (string roomId in roomStates.JoinedRoomIds)
        {
            var stateEvents = await roomLoader.GetRoomStateEvents(roomId, null);
            foreach (var stateEvent in stateEvents)
            {
                updateUserInfo(stateEvent);
            }
        }
        var users = new List<SearchResponse.User>();
        var searchTokens = requestBody.SearchTerm.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string userId in userIds.OrderBy(x => x))
        {
            var user = new SearchResponse.User
            {
                UserId = userId
            };
            if (avatarUrls.TryGetValue(userId, out var avatarUrl))
            {
                user.AvatarUrl = avatarUrl.url;
            }
            if (displayNames.TryGetValue(userId, out var displayName))
            {
                user.DisplayName = displayName.name;
            }
            if (searchTokens.Any(token =>
            {
                return user.UserId.Contains(token) && user.DisplayName?.Contains(token) == true;
            }))
            {
                users.Add(user);
            }
        }
        bool limited = false;
        if (requestBody.Limit is int limit && limit > users.Count)
        {
            users = users.Take(limit).ToList();
            limited = true;
        }
        return new JsonResult(new SearchResponse
        {
            Limited = limited,
            Results = users.ToArray()
        });
    }
}
