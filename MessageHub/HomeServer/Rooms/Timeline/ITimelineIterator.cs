using MessageHub.HomeServer.Events;

namespace MessageHub.HomeServer.Rooms.Timeline;

public interface ITimelineIterator
{
    PersistentDataUnit CurrentEvent { get; }
    ValueTask<bool> TryMoveForwardAsync();
    ValueTask<bool> TryMoveBackwardAsync();
    PersistentDataUnit[] GetStateEvents();
}
