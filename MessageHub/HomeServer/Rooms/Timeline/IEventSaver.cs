using System.Collections.Immutable;
using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms.Timeline;

public interface IEventSaver
{
    Task SaveAsync(
        string roomId,
        string eventId,
        PersistentDataUnit pdu,
        IReadOnlyDictionary<RoomStateKey, string> states);
    Task SaveBatchAsync(
        string roomId,
        IReadOnlyList<string> eventIds,
        IReadOnlyDictionary<string, PersistentDataUnit> events,
        IReadOnlyDictionary<string, ImmutableDictionary<RoomStateKey, string>> states);
    Task SaveInviteAsync(string roomId, IEnumerable<StrippedStateEvent>? states);
    Task RejectInviteAsync(string roomId);
    Task SaveKnockAsync(string roomId, IEnumerable<StrippedStateEvent>? states);
    Task RetractKnockAsync(string roomId);
    Task ForgetAsync(string roomId);
}
