using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class CreateRoomController : ControllerBase
{
    private readonly IPeerIdentity peerIdentity;
    private readonly IAccountData accountData;
    private readonly IEventSender eventSender;
    private readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CreateRoomController(
        IPeerIdentity peerIdentity,
        IAccountData accountData,
        IEventSender eventSender)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);
        ArgumentNullException.ThrowIfNull(eventSender);
        ArgumentNullException.ThrowIfNull(accountData);

        this.peerIdentity = peerIdentity;
        this.accountData = accountData;
        this.eventSender = eventSender;
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
            result = JsonSerializer.SerializeToElement(content);
        }
        else
        {
            var content = creationContent.Value.Deserialize<Dictionary<string, object>>()!;
            content["creator"] = userId;
            content["room_version"] = "9";
            result = JsonSerializer.SerializeToElement(content);
        }
        return result;
    }

    private static JsonElement GetPowerLevelContent(string userId, JsonElement? powerLevelContentOverride)
    {
        var powerLevelContent = new PowerLevelsEvent
        {
            Users = new Dictionary<string, int>
            {
                [userId] = 100
            }.ToImmutableDictionary()
        };
        if (powerLevelContentOverride is null)
        {
            return JsonSerializer.SerializeToElement(powerLevelContent, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
        else
        {
            var propertyMapping = JsonSerializer
                .SerializeToElement(powerLevelContent, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
                .Deserialize<Dictionary<string, JsonElement>>()!;
            var overwriteMapping = JsonSerializer
                .SerializeToElement(powerLevelContentOverride)
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
            return JsonSerializer.SerializeToElement(propertyMapping);
        }
    }

    [Route("createRoom")]
    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreeateRoomParameters parameters)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        if (parameters is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.MissingParameter));
        }

        string roomId = $"!{Guid.NewGuid()}:{peerIdentity.Id}";

        // m.room.create event.
        if (parameters.CreationContent is not null
            && parameters.CreationContent.Value.ValueKind != JsonValueKind.Object)
        {
            return new JsonResult(
                MatrixError.Create(
                    MatrixErrorCode.InvalidParameter,
                    $"{nameof(parameters.CreationContent)}: {parameters.CreationContent}"));
        }
        var roomCreateEventContent = GetRoomCreateContent(userId, parameters.CreationContent, parameters.RoomVersion);
        var (_, error) = await eventSender.SendStateEventAsync(
            userId,
            roomId,
            new RoomStateKey(EventTypes.Create, ""),
            roomCreateEventContent);
        if (error is not null)
        {
            return new JsonResult(error);
        }

        // Join sender.
        var memberEventContent = new MemberEvent
        {
            MemberShip = MembershipStates.Join
        };
        (_, error) = await eventSender.SendStateEventAsync(
            userId,
            roomId,
            new RoomStateKey(EventTypes.Member, userId),
            JsonSerializer.SerializeToElement(memberEventContent, ignoreNullOptions));
        if (error is not null)
        {
            return new JsonResult(error);
        }

        // Power level
        if (parameters.PowerLevelContentOverride is not null
            && parameters.PowerLevelContentOverride.Value.ValueKind != JsonValueKind.Object)
        {
            return new JsonResult(
                MatrixError.Create(
                    MatrixErrorCode.InvalidParameter,
                    $"{nameof(parameters.PowerLevelContentOverride)}: {parameters.PowerLevelContentOverride}"));
        }
        var powerLevelContent = GetPowerLevelContent(userId, parameters.PowerLevelContentOverride);
        (_, error) = await eventSender.SendStateEventAsync(
            userId,
            roomId,
            new RoomStateKey(EventTypes.PowerLevels, ""),
            powerLevelContent);
        if (error is not null)
        {
            return new JsonResult(error);
        }

        // Set alias.
        if (parameters.RoomAliasName is string alias)
        {
            var canonicalAliasContent = new CanonicalAliasEvent { Alias = alias };
            (_, error) = await eventSender.SendStateEventAsync(
                userId,
                roomId,
                new RoomStateKey(EventTypes.CanonicalAlias, ""),
                JsonSerializer.SerializeToElement(canonicalAliasContent, ignoreNullOptions));
            if (error is not null)
            {
                return new JsonResult(error);
            }
        }

        // preset...
        if (parameters.Preset is string preset)
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
                (_, error) = await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(EventTypes.JoinRules, ""),
                    JsonSerializer.SerializeToElement(joinRulesContent, ignoreNullOptions));
                if (error is not null)
                {
                    return new JsonResult(error);
                }
            }
            if (historyVisibilityContent is not null)
            {
                (_, error) = await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(EventTypes.HistoryVisibility, ""),
                    JsonSerializer.SerializeToElement(historyVisibilityContent, ignoreNullOptions));
                if (error is not null)
                {
                    return new JsonResult(error);
                }
            }
        }

        // Initial state events.
        if (parameters.InitialState is not null)
        {
            foreach (var stateEvent in parameters.InitialState)
            {
                (_, error) = await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(stateEvent.EventType, stateEvent.StateKey),
                    stateEvent.Content);
            }
        }

        // Room name
        if (parameters.Name is string name)
        {
            var nameContent = new NameEvent { Name = name };
            (_, error) = await eventSender.SendStateEventAsync(
                userId,
                roomId,
                new RoomStateKey(EventTypes.Name, ""),
                JsonSerializer.SerializeToElement(nameContent, ignoreNullOptions));
            if (error is not null)
            {
                return new JsonResult(error);
            }
        }

        // Topic.
        if (parameters.Topic is string topic)
        {
            var topicContent = new TopicEvent { Topic = topic };
            (_, error) = await eventSender.SendStateEventAsync(
                userId,
                roomId,
                new RoomStateKey(EventTypes.Topic, ""),
                JsonSerializer.SerializeToElement(topicContent, ignoreNullOptions));
            if (error is not null)
            {
                return new JsonResult(error);
            }
        }

        // invite...
        if (parameters.Invite is string[] userIds)
        {
            foreach (var invitedId in userIds)
            {
                var inviteContent = new MemberEvent
                {
                    IsDirect = parameters.IsDirect,
                    MemberShip = MembershipStates.Invite
                };
                (_, error) = await eventSender.SendStateEventAsync(
                    userId,
                    roomId,
                    new RoomStateKey(EventTypes.Member, invitedId),
                    JsonSerializer.SerializeToElement(inviteContent, ignoreNullOptions));
            }
        }

        // Set visibility.
        if (parameters.Visibility is not null)
        {
            await accountData.SetRoomVisibilityAsync(roomId, parameters.Visibility);
        }

        return new JsonResult(new
        {
            room_id = roomId
        });
    }
}
