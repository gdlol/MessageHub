namespace MessageHub.HomeServer.Rooms.Timeline;

public interface ITimelineIterator : IDisposable
{
    string CurrentEventId { get; }
    ValueTask<bool> TryMoveForwardAsync();
    ValueTask<bool> TryMoveBackwardAsync();
}
