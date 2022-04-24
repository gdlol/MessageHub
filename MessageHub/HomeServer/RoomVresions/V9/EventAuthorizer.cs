using System.Collections.Immutable;
using MessageHub.ClientServer.Protocol.Events.Room;

namespace MessageHub.HomeServer.RoomVersions.V9;

public class EventAuthorizer
{
    public RoomIdentifier RoomId { get; }

    public ImmutableDictionary<RoomStateKey, object> States { get; }

    public EventAuthorizer(RoomIdentifier roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        RoomId = roomId;
        States = ImmutableDictionary<RoomStateKey, object>.Empty;
    }

    public EventAuthorizer(RoomIdentifier roomId, IReadOnlyDictionary<RoomStateKey, object> states)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(states);

        RoomId = roomId;
        States = states.ToImmutableDictionary();
    }

    private bool HasCreateEvent => States.ContainsKey(new RoomStateKey(EventTypes.Create, string.Empty));

    private T? TryGetEvent<T>(string eventType, string stateKey)
    {
        if (States.TryGetValue(new RoomStateKey(eventType, stateKey), out var content))
        {
            return (T)content;
        }
        return default;
    }

    private T? TryGetEvent<T>(string eventType) => TryGetEvent<T>(eventType, string.Empty);

    private CreateEvent? TryGetCreateEvent() => TryGetEvent<CreateEvent>(EventTypes.Create);

    private MemberEvent? TryGetMemberEvent(UserIdentifier userId) =>
        TryGetEvent<MemberEvent>(EventTypes.Member, userId.ToString());

    private JoinRulesEvent? TryGetJoinRulesEvent() => TryGetEvent<JoinRulesEvent>(EventTypes.JoinRules);

    private PowerLevelsEvent? TryGetPowerLevelsEvent() => TryGetEvent<PowerLevelsEvent>(EventTypes.PowerLevels);

    private PowerLevelsEvent GetPowerLevelsEventOrDefault() => TryGetPowerLevelsEvent() ?? new PowerLevelsEvent();

    private int GetPowerLevel(UserIdentifier userId)
    {
        if (TryGetPowerLevelsEvent() is PowerLevelsEvent powerLevelsEvent)
        {
            if (powerLevelsEvent.Users?.TryGetValue(userId.ToString(), out int powerLevel) == true)
            {
                return powerLevel;
            }
            return powerLevelsEvent.UsersDefault ?? 0;
        }
        if (TryGetCreateEvent() is not CreateEvent createEvent)
        {
            throw new InvalidOperationException();
        }
        return createEvent.Creator == userId.ToString() ? 100 : 0;
    }

    private int GetRequiredPowerLevel(string eventType, string? stateKey)
    {
        var powerLevelsEvent = TryGetPowerLevelsEvent();
        if (powerLevelsEvent is not null)
        {
            if (powerLevelsEvent.Events?.TryGetValue(eventType, out int powerLevel) == true)
            {
                return powerLevel;
            }
            return stateKey is not null ? powerLevelsEvent.StateDefault : powerLevelsEvent.EventsDefault;
        }
        else
        {
            return 0;
        }
    }

    public bool Authorize(string eventType, string? stateKey, UserIdentifier sender, object content)
    {
        if (!HasCreateEvent && eventType != EventTypes.Create)
        {
            return false;
        }
        if (eventType == EventTypes.Create)
        {
            if (stateKey != string.Empty)
            {
                return false;
            }
            if (TryGetCreateEvent() is not null)
            {
                return false;
            }
            if (RoomId.PeerId != sender.PeerId)
            {
                return false;
            }
            if (content is not CreateEvent createEvent)
            {
                return false;
            }
            if (createEvent.Creator is null)
            {
                return false;
            }
            return true;
        }
        if (eventType == EventTypes.Member)
        {
            if (!UserIdentifier.TryParse(stateKey, out var userId))
            {
                return false;
            }
            if (content is not MemberEvent memberEvent)
            {
                return false;
            }
            if (memberEvent.MemberShip == MembershipStates.Join)
            {
                if (States.Count == 1 && TryGetCreateEvent()?.Creator == stateKey)
                {
                    return true;
                }
                if (sender != userId)
                {
                    return false;
                }
                if (TryGetMemberEvent(sender)?.MemberShip == MembershipStates.Ban)
                {
                    return false;
                }
                if (TryGetJoinRulesEvent() is JoinRulesEvent joinRulesEvent)
                {
                    if (joinRulesEvent.JoinRule == JoinRules.Invite)
                    {
                        if (TryGetMemberEvent(userId) is MemberEvent userMemberEvent
                            && (userMemberEvent.MemberShip == MembershipStates.Invite
                                || userMemberEvent.MemberShip == MembershipStates.Join))
                        {
                            return true;
                        }
                        return false;
                    }
                    else if (joinRulesEvent.JoinRule == JoinRules.Public)
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (memberEvent.MemberShip == MembershipStates.Invite)
            {
                if (TryGetMemberEvent(sender)?.MemberShip != MembershipStates.Join)
                {
                    return false;
                }
                if (TryGetMemberEvent(userId) is MemberEvent userMemberEvent
                    && (userMemberEvent.MemberShip == MembershipStates.Join
                        || userMemberEvent.MemberShip == MembershipStates.Ban))
                {
                    return false;
                }
                int senderPowerLevel = GetPowerLevel(sender);
                int invitePowerLevel = GetPowerLevelsEventOrDefault().Invite;
                if (senderPowerLevel >= invitePowerLevel)
                {
                    return true;
                }
                return false;
            }
            else if (memberEvent.MemberShip == MembershipStates.Leave)
            {
                if (sender == userId)
                {
                    if (TryGetMemberEvent(userId) is MemberEvent userMemberEvent
                        && (userMemberEvent.MemberShip == MembershipStates.Invite
                            || userMemberEvent.MemberShip == MembershipStates.Join
                            || userMemberEvent.MemberShip == MembershipStates.Knock))
                    {
                        return true;
                    }
                    return false;
                }
                if (TryGetMemberEvent(sender)?.MemberShip != MembershipStates.Join)
                {
                    return false;
                }
                int senderPowerLevel = GetPowerLevel(sender);
                if (TryGetMemberEvent(userId)?.MemberShip == MembershipStates.Ban)
                {
                    int banPowerLevel = GetPowerLevelsEventOrDefault().Ban;
                    if (senderPowerLevel < banPowerLevel)
                    {
                        return false;
                    }
                }
                int kickPowerLevel = GetPowerLevelsEventOrDefault().Kick;
                if (senderPowerLevel >= kickPowerLevel)
                {
                    int userPowerLevel = GetPowerLevel(userId);
                    if (senderPowerLevel > userPowerLevel)
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (memberEvent.MemberShip == MembershipStates.Ban)
            {
                if (TryGetMemberEvent(sender)?.MemberShip != MembershipStates.Join)
                {
                    return false;
                }
                int senderPowerLevel = GetPowerLevel(sender);
                int banPowerLevel = GetPowerLevelsEventOrDefault().Ban;
                if (senderPowerLevel >= banPowerLevel)
                {
                    int userPowerLevel = GetPowerLevel(userId);
                    if (senderPowerLevel > userPowerLevel)
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (memberEvent.MemberShip == MembershipStates.Knock)
            {
                if (TryGetJoinRulesEvent()?.JoinRule != JoinRules.Knock)
                {
                    return false;
                }
                if (sender != userId)
                {
                    return false;
                }
                string? senderMembership = TryGetMemberEvent(sender)?.MemberShip;
                if (!(senderMembership == MembershipStates.Ban
                      || senderMembership == MembershipStates.Invite
                      || senderMembership == MembershipStates.Join))
                {
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }
        if (TryGetMemberEvent(sender)?.MemberShip != MembershipStates.Join)
        {
            return false;
        }
        {
            int senderPowerLevel = GetPowerLevel(sender);
            int requiredPowerLevel = GetRequiredPowerLevel(eventType, stateKey);
            if (senderPowerLevel >= requiredPowerLevel)
            {
                return true;
            }
        }
        if (stateKey?.StartsWith("@") == true && stateKey != sender.ToString())
        {
            return false;
        }
        if (eventType == EventTypes.PowerLevels)
        {
            if (content is not PowerLevelsEvent powerLevelsEvent)
            {
                return false;
            }
            if (powerLevelsEvent.Users is not null)
            {
                foreach (string key in powerLevelsEvent.Users.Keys)
                {
                    if (!UserIdentifier.TryParse(key, out _))
                    {
                        return false;
                    }
                }
            }
            var oldPowerLevelsEvent = TryGetPowerLevelsEvent();
            if (oldPowerLevelsEvent is null)
            {
                return true;
            }
            int senderPowerLevel = GetPowerLevel(sender);
            foreach (var (oldValue, newValue) in new[]
            {
                (oldPowerLevelsEvent.UsersDefault, powerLevelsEvent.UsersDefault),
                (oldPowerLevelsEvent.EventsDefault, powerLevelsEvent.EventsDefault),
                (oldPowerLevelsEvent.StateDefault, powerLevelsEvent.StateDefault),
                (oldPowerLevelsEvent.Ban, powerLevelsEvent.Ban),
                (oldPowerLevelsEvent.Kick, powerLevelsEvent.Kick),
                (oldPowerLevelsEvent.Invite, powerLevelsEvent.Invite)
            })
            {
                if (oldValue > senderPowerLevel || newValue > senderPowerLevel)
                {
                    return false;
                }
            }
            foreach (var (oldMapping, newMapping) in new[]
            {
                (oldPowerLevelsEvent.Events, powerLevelsEvent.Events),
                (oldPowerLevelsEvent.Users, powerLevelsEvent.Users),
                (oldPowerLevelsEvent.Notifications, powerLevelsEvent.Notifications)
            })
            {
                var keys = new HashSet<string>();
                if (oldMapping is not null)
                {
                    keys.UnionWith(oldMapping.Keys);
                }
                if (newMapping is not null)
                {
                    keys.UnionWith(newMapping.Keys);
                }
                foreach (string key in keys)
                {
                    int? oldValue = null;
                    int? newValue = null;
                    if (oldMapping?.TryGetValue(key, out int value) == true)
                    {
                        oldValue = value;
                    }
                    if (newMapping?.TryGetValue(key, out value) == true)
                    {
                        newValue = value;
                    }
                    if (oldValue != newValue)
                    {
                        if (oldValue > senderPowerLevel || newValue > senderPowerLevel)
                        {
                            return false;
                        }
                    }
                }
            }
            if (oldPowerLevelsEvent.Users is not null)
            {
                foreach (var (userId, powerLevel) in oldPowerLevelsEvent.Users)
                {
                    if (userId != sender.ToString() && powerLevel == senderPowerLevel)
                    {
                        int? newPowerLevel = null;
                        if (powerLevelsEvent.Users?.TryGetValue(userId, out int value) == true)
                        {
                            newPowerLevel = value;
                        }
                        if (powerLevel != newPowerLevel)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        return false;
    }

    public (bool, EventAuthorizer) TryUpdateState(RoomStateKey roomStateKey, UserIdentifier sender, object content)
    {
        if (Authorize(roomStateKey.EventType, roomStateKey.StateKey, sender, content))
        {
            var newStates = States.SetItem(roomStateKey, content);
            return (true, new EventAuthorizer(RoomId, newStates));
        }
        else
        {
            return (false, this);
        }
    }
}
