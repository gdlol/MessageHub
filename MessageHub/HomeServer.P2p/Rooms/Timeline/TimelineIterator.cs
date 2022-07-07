using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Rooms.Timeline;

internal sealed class TimelineIterator : ITimelineIterator
{
    private readonly EventStoreSession session;
    private readonly string roomId;
    private readonly bool ownsSession;

    public string CurrentEventId { get; private set; }

    public TimelineIterator(EventStoreSession session, string roomId, string currentEventId, bool ownsSession = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(currentEventId);

        this.session = session;
        this.roomId = roomId;
        CurrentEventId = currentEventId;
        this.ownsSession = ownsSession;
    }

    public async ValueTask<bool> TryMoveBackwardAsync()
    {
        var currentRecord = await session.GetTimelineRecordAsync(roomId, CurrentEventId);
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
        var currentRecord = await session.GetTimelineRecordAsync(roomId, CurrentEventId);
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
        if (ownsSession)
        {
            session.Dispose();
        }
    }
}
