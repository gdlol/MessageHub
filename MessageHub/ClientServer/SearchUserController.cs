using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class SearchUserController : ControllerBase
{
    private readonly ILogger logger;
    private readonly ITimelineLoader timelineLoader;
    private readonly IRooms rooms;
    private readonly IUserDiscoveryService userDiscoveryService;

    public SearchUserController(
        ILogger<SearchUserController> logger,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        IUserDiscoveryService userDiscoveryService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(userDiscoveryService);

        this.logger = logger;
        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
        this.userDiscoveryService = userDiscoveryService;
    }

    [Route("user_directory/search")]
    [HttpPost]
    public async Task<IActionResult> SearchUser([FromBody] SearchUserRequest request)
    {
        if (string.IsNullOrEmpty(request.SearchTerm))
        {
            return BadRequest(MatrixError.Create(MatrixErrorCode.InvalidParameter, nameof(request.SearchTerm)));
        }
        logger.LogDebug("Searching users with term: {}", request.SearchTerm);

        var avatarUrls = new ConcurrentDictionary<string, (string url, long timestamp)>();
        var displayNames = new ConcurrentDictionary<string, (string name, long timestamp)>();
        var userIds = new HashSet<string>();
        void updateUserInfo(PersistentDataUnit stateEvent)
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
        var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
        foreach (string roomId in batchStates.JoinedRoomIds)
        {
            if (batchStates.RoomEventIds.TryGetValue(roomId, out string? eventId))
            {
                using var roomEventStore = await rooms.GetRoomEventStoreAsync(roomId);
                var stateEvents = await roomEventStore.LoadStateEventsAsync(eventId);
                foreach (var stateEvent in stateEvents.Values)
                {
                    updateUserInfo(stateEvent);
                }
            }
        }
        var users = new Dictionary<string, SearchUserResponse.User>();
        var searchTokens = request.SearchTerm.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string userId in userIds.OrderBy(x => x))
        {
            var user = new SearchUserResponse.User
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
                return user.UserId.Contains(token) || user.DisplayName?.Contains(token) == true;
            }))
            {
                users.TryAdd(user.UserId, user);
            }
        }
        bool limited = false;
        {
            if (!(request.Limit is int limit && limit > users.Count))
            {
                // Find remote users.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var remoteUsers = new ConcurrentBag<SearchUserResponse.User>();
                try
                {
                    logger.LogDebug("Searching remote users: {}", request.SearchTerm);
                    var remoteIdentities = await userDiscoveryService.SearchUsersAsync(
                        request.SearchTerm,
                        cts.Token);
                    await Parallel.ForEachAsync(
                        remoteIdentities,
                        new ParallelOptions
                        {
                            CancellationToken = cts.Token,
                            MaxDegreeOfParallelism = 3
                        },
                        async (identity, token) =>
                        {
                            logger.LogDebug("Remote identity: {}", identity.Id);

                            string? avatarUrl = null;
                            string? displayName = null;
                            var userId = UserIdentifier.FromId(identity.Id).ToString();
                            try
                            {
                                var profile = await userDiscoveryService.GetUserProfileAsync(userId, token);
                                (avatarUrl, displayName) = profile;
                                remoteUsers.Add(new SearchUserResponse.User
                                {
                                    AvatarUrl = avatarUrl,
                                    DisplayName = displayName,
                                    UserId = UserIdentifier.FromId(identity.Id).ToString()
                                });
                                cts.CancelAfter(TimeSpan.FromSeconds(1));
                            }
                            catch (OperationCanceledException) { }
                            catch (Exception ex)
                            {
                                logger.LogInformation(ex, "Error getting user profile from {}: {}", identity.Id);
                            }
                        });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error searching remote users");
                }
                foreach (var user in remoteUsers)
                {
                    users.TryAdd(user.UserId!, user);
                }
            }
        }
        {
            if (request.Limit is int limit && limit > users.Count)
            {
                users = users.OrderBy(x => x.Key).Take(limit).ToDictionary(x => x.Key, x => x.Value);
                limited = true;
            }
        }
        logger.LogDebug("Found {} users: {}", users.Count, JsonSerializer.SerializeToElement(users.Values));
        return new JsonResult(new SearchUserResponse
        {
            Limited = limited,
            Results = users.Values.ToArray()
        });
    }
}
