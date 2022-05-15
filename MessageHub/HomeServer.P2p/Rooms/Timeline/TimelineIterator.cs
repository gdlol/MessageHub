using MessageHub.HomeServer.P2p.Providers;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

public sealed class TimelineIterator : ITimelineIterator
{
    private readonly IKeyValueStore store;
    private readonly string roomId;
    private readonly bool ownsStore;

    public string CurrentEventId { get; private set; }

    public TimelineIterator(IKeyValueStore store, string roomId, string currentEventId, bool ownsStore = true)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(currentEventId);

        this.store = store;
        this.roomId = roomId;
        CurrentEventId = currentEventId;
        this.ownsStore = ownsStore;
    }

    public async ValueTask<bool> TryMoveBackwardAsync()
    {
        var currentRecord = await EventStore.GetTimelineRecordAsync(store, roomId, CurrentEventId);
        if (currentRecord is null)
        {
            throw new InvalidOperationException();
        }
        if (currentRecord.PreviousEventId is null)
        {
            return false;
        }
        else
        {
            CurrentEventId = currentRecord.PreviousEventId;
            return true;
        }
    }

    public async ValueTask<bool> TryMoveForwardAsync()
    {
        var currentRecord = await EventStore.GetTimelineRecordAsync(store, roomId, CurrentEventId);
        if (currentRecord is null)
        {
            throw new InvalidOperationException();
        }
        if (currentRecord.NextEventId is null)
        {
            return false;
        }
        else
        {
            CurrentEventId = currentRecord.NextEventId;
            return true;
        }
    }

    public void Dispose()
    {
        if (ownsStore)
        {
            store.Dispose();
        }
    }
}
