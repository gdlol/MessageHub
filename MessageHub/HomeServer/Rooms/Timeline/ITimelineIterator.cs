namespace MessageHub.HomeServer.Rooms.Timeline;

public interface ITimelineIterator
{
    string CurrentEventId { get; }
    ValueTask<bool> TryMoveForwardAsync();
    ValueTask<bool> TryMoveBackwardAsync();
}
