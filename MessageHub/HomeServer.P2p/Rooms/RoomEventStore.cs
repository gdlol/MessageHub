using System.Collections.Immutable;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;

namespace MessageHub.HomeServer.P2p.Rooms;

internal sealed class RoomEventStore : IRoomEventStore
{
    private readonly EventStoreSession session;
    private readonly string roomId;
    private readonly bool ownsSession;

    public string Creator { get; }

    public RoomEventStore(EventStoreSession session, string roomId, bool ownsSession = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(roomId);

        this.session = session;
        this.roomId = roomId;
        this.ownsSession = ownsSession;
        Creator = session.State.RoomCreators[roomId];
    }

    public async Task<string[]> GetMissingEventIdsAsync(IEnumerable<string> eventIds)
    {
        var result = new List<string>();
        foreach (string eventId in eventIds)
        {
            var value = await session.GetEventAsync(roomId, eventId);
            if (value is null)
            {
                result.Add(eventId);
            }
        }
        return result.ToArray();
    }

    public async ValueTask<PersistentDataUnit> LoadEventAsync(string eventId)
    {
        var value = await session.GetEventAsync(roomId, eventId);
        if (value is null)
        {
            throw new KeyNotFoundException(eventId);
        }
        return value;
    }

    public async ValueTask<ImmutableDictionary<RoomStateKey, string>> LoadStatesAsync(string eventId)
    {
        var value = await session.GetStatesAsync(roomId, eventId);
        if (value is null)
        {
            throw new KeyNotFoundException(eventId);
        }
        return value;
    }

    public void Dispose()
    {
        if (ownsSession)
        {
            session.Dispose();
        }
    }
}
