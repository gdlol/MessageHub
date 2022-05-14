using System.Text;
using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

public sealed class TimelineIterator : ITimelineIterator
{
    private readonly ILogIterator iterator;

    public TimelineIterator(ILogIterator iterator)
    {
        ArgumentNullException.ThrowIfNull(iterator);

        this.iterator = iterator;
    }

    public string CurrentEventId => Encoding.UTF8.GetString(iterator.CurrentValue.Span);

    public ValueTask<bool> TryMoveBackwardAsync()
    {
        return iterator.TryMoveBackwardAsync();
    }

    public ValueTask<bool> TryMoveForwardAsync()
    {
        return iterator.TryMoveForwardAsync();
    }

    public void Dispose()
    {
        iterator.Dispose();
    }
}
