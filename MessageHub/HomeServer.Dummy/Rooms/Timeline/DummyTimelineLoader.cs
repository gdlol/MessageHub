using System.Collections.Immutable;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.Dummy.Rooms.Timeline;

public class DummyTimelineLoader : ITimelineLoader
{
    public bool IsEmpty => DummyTimeline.BatchIds.IsEmpty;

    public string CurrentBatchId => DummyTimeline.BatchIds.LastOrDefault() ?? string.Empty;

    public Task<IReadOnlyDictionary<string, string>> GetRoomEventIds(string? batchId)
    {
        IReadOnlyDictionary<string, string> result;
        if (batchId is null)
        {
            result = ImmutableDictionary<string, string>.Empty;
        }
        else
        {
            result = DummyTimeline.RoomEventIds[batchId];
        }
        return Task.FromResult(result);
    }

    public Task<ITimelineIterator?> GetTimelineIteratorAsync(string roomId, string eventId)
    {
        ITimelineIterator? result = null;
        if (DummyTimeline.Timelines.TryGetValue(roomId, out var timeline))
        {
            result = new DummyTimelineIterator(timeline, timeline.IndexOf(eventId));
        }
        return Task.FromResult(result);
    }

    public Task<BatchStates> LoadBatchStatesAsync(Func<string, bool> roomIdFilter, bool includeLeave)
    {
        BatchStates batchStates = DummyTimeline.BatchStates.Filter(roomIdFilter, includeLeave);
        return Task.FromResult(batchStates);
    }
}
