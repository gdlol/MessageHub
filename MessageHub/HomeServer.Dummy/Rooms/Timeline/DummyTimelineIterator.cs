using System.Collections.Immutable;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

internal class DummyTimelineIterator : ITimelineIterator
{
    private readonly ImmutableList<string> eventIds;
    private int index;

    public string CurrentEventId { get; private set; }

    public DummyTimelineIterator(ImmutableList<string> eventIds, int index)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        this.eventIds = eventIds;
        this.index = index;
        CurrentEventId = eventIds[index];
    }

    public ValueTask<bool> TryMoveForwardAsync()
    {
        if (index >= eventIds.Count - 1)
        {
            return ValueTask.FromResult(false);
        }
        index += 1;
        CurrentEventId = eventIds[index];
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> TryMoveBackwardAsync()
    {
        if (index <= 0)
        {
            return ValueTask.FromResult(false);
        }
        index -= 1;
        CurrentEventId = eventIds[index];
        return ValueTask.FromResult(true);
    }

    public void Dispose() { }
}
