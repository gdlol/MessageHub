using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.ClientServer.Protocol.Events.Room;
using MessageHub.Federation;
using MessageHub.HomeServer.Formatting;

namespace MessageHub.HomeServer;

public class RoomEventsReceiver
{
    private readonly string roomId;
    private readonly IPeerIdentity identity;
    private readonly IPeerIdentityResolver peerIdentityResolver;
    private readonly IRoomEventStore roomEventStore;
    private readonly IMissingEventsResolver missingEventsResolver;

    public RoomEventsReceiver(
        string roomId,
        IPeerIdentity identity,
        IPeerIdentityResolver peerIdentityResolver,
        IRoomEventStore roomEventStore,
        IMissingEventsResolver missingEventsResolver)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peerIdentityResolver);
        ArgumentNullException.ThrowIfNull(roomEventStore);
        ArgumentNullException.ThrowIfNull(missingEventsResolver);

        this.roomId = roomId;
        this.identity = identity;
        this.peerIdentityResolver = peerIdentityResolver;
        this.roomEventStore = roomEventStore;
        this.missingEventsResolver = missingEventsResolver;
    }

    private bool Validate(PersistentDataUnit pdu)
    {
        if (!UserIdentifier.TryParse(pdu.Sender, out var senderIdentifier))
        {
            return false;
        }
        if (senderIdentifier.PeerId != pdu.Origin)
        {
            return false;
        }
        return true;
    }

    private async ValueTask<bool> VerifySignatureAsync(PersistentDataUnit pdu)
    {
        var originIdentity = await peerIdentityResolver.ResolveAsync(pdu.Origin, pdu.OriginServerTimestamp);
        return identity.VerifyJson(originIdentity, pdu.ToJsonElement());
    }

    private bool VerifyHashAsync(PersistentDataUnit pdu)
    {
        if (pdu.Hashes.SingleOrDefault() is not (string algorithm, string hash)
            || algorithm != "sha256"
            || hash != UnpaddedBase64Encoder.Encode(EventHash.ComputeHash(pdu)))
        {
            return false;
        }
        return true;
    }

    private async ValueTask<bool> AuthorizeEventAsync(PersistentDataUnit pdu)
    {
        if (pdu.PreviousEvents.Length == 0)
        {
            return false;
        }
        ImmutableDictionary<RoomStateKey, string> states;
        if (pdu.PreviousEvents.Length == 1)
        {
            states = await roomEventStore.LoadStatesAsync(pdu.PreviousEvents[0]);
        }
        else
        {
            var branchStates = new List<ImmutableDictionary<RoomStateKey, string>>();
            foreach (string eventId in pdu.PreviousEvents)
            {
                
            }
        }
        return true;
    }

    private async ValueTask<string?> ReceiveResolvedEvent(PersistentDataUnit pdu)
    {
        // Check auth events.
        if (pdu.PreviousEvents.Length == 0)
        {
            return $"{nameof(pdu.PreviousEvents)}: {JsonSerializer.Serialize(pdu.PreviousEvents)}";
        }
        if (pdu.PreviousEvents.Length == 1)
        {
            var previousEventId = pdu.PreviousEvents[0];
            var previousStates = await roomEventStore.LoadStatesAsync(previousEventId);
            var createEventId = previousStates[new RoomStateKey(EventTypes.Create, string.Empty)];
            if (!pdu.AuthorizationEvents.Contains(createEventId))
            {
                return $"{nameof(createEventId)}: {createEventId}";
            }
            if (previousStates.TryGetValue(
                new RoomStateKey(EventTypes.PowerLevels, string.Empty),
                out string? powerLevelEventId)
                && !pdu.AuthorizationEvents.Contains(powerLevelEventId))
            {
                return $"{nameof(powerLevelEventId)}: {powerLevelEventId}";
            }
            if (previousStates.TryGetValue(
                new RoomStateKey(EventTypes.Member, pdu.Sender),
                out string? memberEventId)
                && !pdu.AuthorizationEvents.Contains(memberEventId))
            {
                return $"{nameof(memberEventId)}: {memberEventId}";
            }
            if (pdu.EventType == EventTypes.Member)
            {
                if (pdu.StateKey is null)
                {
                    return nameof(pdu.StateKey);
                }
                if (previousStates.TryGetValue(
                    new RoomStateKey(EventTypes.Member, pdu.StateKey),
                    out string? targetMemberEventId)
                    && !pdu.AuthorizationEvents.Contains(targetMemberEventId))
                {
                    return $"{nameof(targetMemberEventId)}: {targetMemberEventId}";
                }
                if (targetMemberEventId is not null)
                {
                    var targetMemberEvent = await roomEventStore.LoadEventAsync(targetMemberEventId);
                    var targetMemberEventContent = JsonSerializer.Deserialize<MemberEvent>(targetMemberEvent.Content)!;
                    if (targetMemberEventContent.MemberShip == MembershipStates.Join
                        || targetMemberEventContent.MemberShip == MembershipStates.Invite)
                    {
                        if (previousStates.TryGetValue(
                            new RoomStateKey(EventTypes.JoinRules, string.Empty),
                            out string? joinRulesEventId)
                            && !pdu.AuthorizationEvents.Contains(joinRulesEventId))
                        {
                            return $"{nameof(joinRulesEventId)}: {joinRulesEventId}";
                        }
                    }
                }
            }
        }
        else
        {
            // ...
        }
        return null;
    }

    public async Task<Dictionary<string, string?>> ReceiveEvents(PersistentDataUnit[] pdus)
    {
        var errors = new Dictionary<string, string?>();
        var events = new Dictionary<string, PersistentDataUnit>();
        foreach (var pdu in pdus)
        {
            if (pdu.RoomId != roomId)
            {
                continue;
            }
            var eventId = pdu.GetEventId();
            errors[eventId] = null;
            events[eventId] = pdu;
        }
        var previousEventIds = pdus.SelectMany(pdu => pdu.PreviousEvents).Distinct().ToArray();
        var missingEventIds = await roomEventStore.GetMissingEventIdsAsync(previousEventIds);
        await missingEventsResolver.ResolveMessingEventsAsync(roomId, missingEventIds);

        return errors;
    }
}
