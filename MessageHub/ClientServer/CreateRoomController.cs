using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Authentication;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Remote;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class CreateRoomController : ControllerBase
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserProfile userProfile;
    private readonly IUserDiscoveryService userDiscoveryService;
    private readonly IAccountData accountData;
    private readonly IEventSaver eventSaver;
    private readonly IRemoteRooms remoteRooms;
    private readonly IEventPublisher eventPublisher;
    private readonly IRoomDiscoveryService roomDiscoveryService;

    public CreateRoomController(
        ILogger<CreateRoomController> logger,
        IIdentityService identityService,
        IUserProfile userProfile,
        IUserDiscoveryService userDiscoveryService,
        IAccountData accountData,
        IEventSaver eventSaver,
        IRemoteRooms remoteRooms,
        IEventPublisher eventPublisher,
        IRoomDiscoveryService roomDiscoveryService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(userDiscoveryService);
        ArgumentNullException.ThrowIfNull(accountData);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(remoteRooms);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(roomDiscoveryService);

        this.logger = logger;
        this.identityService = identityService;
        this.userProfile = userProfile;
        this.userDiscoveryService = userDiscoveryService;
        this.accountData = accountData;
        this.eventSaver = eventSaver;
        this.remoteRooms = remoteRooms;
        this.eventPublisher = eventPublisher;
        this.roomDiscoveryService = roomDiscoveryService;
    }

    private static JsonElement GetRoomCreateContent(string userId, JsonElement? creationContent, string? _)
    {
        JsonElement result;
        if (creationContent is null)
        {
            var content = new CreateEvent
            {
                Creator = userId,
                RoomVersion = "9"
            };
            result = JsonSerializer.SerializeToElement(content, ignoreNullOptions);
        }
        else
        {
            var content = creationContent.Value.Deserialize<Dictionary<string, object>>()!;
            content["creator"] = userId;
            content["room_version"] = "9";
            result = JsonSerializer.SerializeToElement(content, ignoreNullOptions);
        }
        return result;
    }

    private static JsonElement GetPowerLevelContent(string userId, CreateRoomRequest request)
    {
        PowerLevelsEvent powerLevelContent;
        if (request.IsDirect == true && request.Invite is string[] inviteIds)
        {
            powerLevelContent = new PowerLevelsEvent
            {
                Users = new[] { userId }.Concat(inviteIds).ToImmutableDictionary(id => id, _ => 100)
            };
        }
        else
        {
            powerLevelContent = new PowerLevelsEvent
            {
                Users = new Dictionary<string, int>
                {
                    [userId] = 100
                }.ToImmutableDictionary()
            };
        }
        if (request.PowerLevelContentOverride is null)
        {
            return JsonSerializer.SerializeToElement(powerLevelContent, ignoreNullOptions);
        }
        else
        {
            var propertyMapping = JsonSerializer
                .SerializeToElement(powerLevelContent, ignoreNullOptions)
                .Deserialize<Dictionary<string, JsonElement>>()!;
            var overwriteMapping = request.PowerLevelContentOverride.Value
                .Deserialize<Dictionary<string, JsonElement>>()!;
            var overwriteKeys = new List<string>();
            foreach (var (key, value) in propertyMapping)
            {
                if (overwriteMapping.TryGetValue(key, out var overwriteValue)
                    && value.ValueKind == overwriteValue.ValueKind)
                {
                    overwriteKeys.Add(key);
                }
            }
            foreach (var key in overwriteKeys)
            {
                propertyMapping[key] = overwriteMapping[key];
            }
            return JsonSerializer.SerializeToElement(propertyMapping, ignoreNullOptions);
        }
    }

    [Route("createRoom")]
    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        var senderId = UserIdentifier.Parse(userId);
        if (request is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.MissingParameter));
        }
        if (request.CreationContent is not null
            && request.CreationContent.Value.ValueKind != JsonValueKind.Object)
        {
            return new JsonResult(
                MatrixError.Create(
                    MatrixErrorCode.InvalidParameter,
                    $"{nameof(request.CreationContent)}: {request.CreationContent}"));
        }
        var roomCreateEventContent = GetRoomCreateContent(userId, request.CreationContent, request.RoomVersion);
        if (request.PowerLevelContentOverride is not null)
        {
            try
            {
                request.PowerLevelContentOverride.Value.Deserialize<PowerLevelsEvent>();
            }
            catch (Exception)
            {
                return new JsonResult(
                    MatrixError.Create(
                        MatrixErrorCode.InvalidParameter,
                        $"{nameof(request.PowerLevelContentOverride)}: {request.PowerLevelContentOverride}"));
            }
        }
        var identity = identityService.GetSelfIdentity();

        string roomId = $"!{Guid.NewGuid()}:{identity.Id}";

        var roomSnapshot = new RoomSnapshot();
        PersistentDataUnit pdu;
        var eventIds = new List<string>();
        var events = new Dictionary<string, PersistentDataUnit>();
        var statesMap = new Dictionary<string, ImmutableDictionary<RoomStateKey, string>>();
        void AddEvent(PersistentDataUnit pdu, ImmutableDictionary<RoomStateKey, string> states)
        {
            pdu = identity.SignEvent(pdu);
            string eventId = EventHash.GetEventId(pdu);
            eventIds.Add(eventId);
            events[eventId] = pdu;
            statesMap[eventId] = states;
        }
        var serverKeys = identity.GetServerKeys();

        // m.room.create event.
        (roomSnapshot, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.Create,
            stateKey: string.Empty,
            serverKeys: serverKeys,
            sender: senderId,
            content: roomCreateEventContent,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        AddEvent(pdu, roomSnapshot.States);

        // Join sender.
        string? avatarUrl = await userProfile.GetAvatarUrlAsync(userId);
        string? displayName = await userProfile.GetDisplayNameAsync(userId);
        (roomSnapshot, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.Member,
            stateKey: userId,
            serverKeys: serverKeys,
            sender: senderId,
            content: JsonSerializer.SerializeToElement(new MemberEvent
            {
                AvatarUrl = avatarUrl,
                DisplayName = displayName,
                MemberShip = MembershipStates.Join
            },
            ignoreNullOptions),
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        AddEvent(pdu, roomSnapshot.States);

        // Set power levels.
        var powerLevelContent = GetPowerLevelContent(userId, request);
        (roomSnapshot, pdu) = EventCreation.CreateEvent(
            roomId: roomId,
            snapshot: roomSnapshot,
            eventType: EventTypes.PowerLevels,
            stateKey: string.Empty,
            serverKeys: serverKeys,
            sender: senderId,
            content: powerLevelContent,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        AddEvent(pdu, roomSnapshot.States);

        // Set alias.
        if (request.RoomAliasName is string alias)
        {
            var roomAlias = new RoomAlias(alias, identity.Id);
            (roomSnapshot, pdu) = EventCreation.CreateEvent(
                roomId: roomId,
                snapshot: roomSnapshot,
                eventType: EventTypes.CanonicalAlias,
                stateKey: string.Empty,
                serverKeys: serverKeys,
                sender: senderId,
                content: JsonSerializer.SerializeToElement(
                    new CanonicalAliasEvent { Alias = roomAlias.ToString() },
                    ignoreNullOptions),
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            AddEvent(pdu, roomSnapshot.States);
            await roomDiscoveryService.SetRoomAliasAsync(roomId, roomAlias.ToString());
        }

        // Presets.
        if (request.Preset is string preset)
        {
            JoinRulesEvent? joinRulesContent = null;
            HistoryVisibilityEvent? historyVisibilityContent = null;
            if (preset == "private_chat" || preset == "trusted_private_chat")
            {
                joinRulesContent = new JoinRulesEvent { JoinRule = JoinRules.Invite };
                historyVisibilityContent = new HistoryVisibilityEvent
                {
                    HistoryVisibility = HistoryVisibilityKinds.Shared
                };
            }
            else if (preset == "public_chat")
            {
                joinRulesContent = new JoinRulesEvent { JoinRule = JoinRules.Public };
                historyVisibilityContent = new HistoryVisibilityEvent
                {
                    HistoryVisibility = HistoryVisibilityKinds.Shared
                };
            }
            if (joinRulesContent is not null)
            {
                (roomSnapshot, pdu) = EventCreation.CreateEvent(
                    roomId: roomId,
                    snapshot: roomSnapshot,
                    eventType: EventTypes.JoinRules,
                    stateKey: string.Empty,
                    serverKeys: serverKeys,
                    sender: senderId,
                    content: JsonSerializer.SerializeToElement(joinRulesContent, ignoreNullOptions),
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                AddEvent(pdu, roomSnapshot.States);
            }
            if (historyVisibilityContent is not null)
            {
                (roomSnapshot, pdu) = EventCreation.CreateEvent(
                    roomId: roomId,
                    snapshot: roomSnapshot,
                    eventType: EventTypes.HistoryVisibility,
                    stateKey: string.Empty,
                    serverKeys: serverKeys,
                    sender: senderId,
                    content: JsonSerializer.SerializeToElement(historyVisibilityContent, ignoreNullOptions),
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                AddEvent(pdu, roomSnapshot.States);
            }
        }

        // Initial state events.
        if (request.InitialState is not null)
        {
            foreach (var stateEvent in request.InitialState)
            {
                (roomSnapshot, pdu) = EventCreation.CreateEvent(
                    roomId: roomId,
                    snapshot: roomSnapshot,
                    eventType: stateEvent.EventType,
                    stateKey: stateEvent.StateKey,
                    serverKeys: serverKeys,
                    sender: senderId,
                    content: stateEvent.Content,
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                AddEvent(pdu, roomSnapshot.States);
            }
        }

        // Room name.
        if (request.Name is string name)
        {
            (roomSnapshot, pdu) = EventCreation.CreateEvent(
                roomId: roomId,
                snapshot: roomSnapshot,
                eventType: EventTypes.Name,
                stateKey: string.Empty,
                serverKeys: serverKeys,
                sender: senderId,
                content: JsonSerializer.SerializeToElement(
                    new NameEvent { Name = name },
                    ignoreNullOptions),
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            AddEvent(pdu, roomSnapshot.States);
        }

        // Topic.
        if (request.Topic is string topic)
        {
            (roomSnapshot, pdu) = EventCreation.CreateEvent(
                roomId: roomId,
                snapshot: roomSnapshot,
                eventType: EventTypes.Topic,
                stateKey: string.Empty,
                serverKeys: serverKeys,
                sender: senderId,
                content: JsonSerializer.SerializeToElement(
                    new TopicEvent { Topic = topic },
                    ignoreNullOptions),
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            AddEvent(pdu, roomSnapshot.States);
        }

        await eventSaver.SaveBatchAsync(roomId, eventIds, events, statesMap);

        // Invites.
        if (request.Invite is string[] userIds)
        {
            foreach (var invitedId in userIds)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var token = cts.Token;
                try
                {
                    (avatarUrl, displayName) = await userDiscoveryService.GetUserProfileAsync(invitedId, token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Timeout getting user profile from {}", invitedId);
                    continue;
                }
                (roomSnapshot, pdu) = EventCreation.CreateEvent(
                    roomId: roomId,
                    snapshot: roomSnapshot,
                    eventType: EventTypes.Member,
                    stateKey: invitedId,
                    serverKeys: serverKeys,
                    sender: senderId,
                    content: JsonSerializer.SerializeToElement(
                        new MemberEvent
                        {
                            AvatarUrl = avatarUrl,
                            DisplayName = displayName,
                            IsDirect = request.IsDirect,
                            MemberShip = MembershipStates.Invite
                        },
                        ignoreNullOptions),
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                pdu = identity.SignEvent(pdu);
                string eventId = EventHash.GetEventId(pdu);
                var inviteRoomState = events.Values.Select(pdu => new StrippedStateEvent
                {
                    Content = pdu.Content,
                    EventType = pdu.EventType,
                    Sender = pdu.Sender,
                    StateKey = pdu.StateKey!
                })
                .Where(x => (x.EventType, x.StateKey) != (EventTypes.Member, invitedId))
                .ToList();
                inviteRoomState.Add(new StrippedStateEvent
                {
                    Content = pdu.Content,
                    EventType = pdu.EventType,
                    Sender = pdu.Sender,
                    StateKey = pdu.StateKey!
                });
                var remoteInviteRequest = new Federation.Protocol.InviteRequest
                {
                    Event = pdu,
                    InviteRoomState = inviteRoomState.ToArray(),
                    RoomVersion = 9
                };
                logger.LogInformation("Sending invite to {}...", invitedId);
                await remoteRooms.InviteAsync(roomId, eventId, remoteInviteRequest);
                var signedPdu = identity.SignEvent(pdu);
                await eventSaver.SaveAsync(roomId, eventId, pdu, roomSnapshot.States);
                await eventPublisher.PublishAsync(pdu);
            }
        }

        // Set visibility.
        if (request.Visibility is not null)
        {
            await accountData.SetRoomVisibilityAsync(roomId, request.Visibility);
        }

        return new JsonResult(new { room_id = roomId });
    }
}
