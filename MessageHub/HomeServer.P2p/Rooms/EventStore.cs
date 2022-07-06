using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Rooms;

internal class EventStore
{
    private readonly IStorageProvider storageProvider;
    private EventStoreState? state = null;
    private readonly ManualResetEvent locker = new(initialState: true);

    public EventStore(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.storageProvider = storageProvider;
    }

    public EventStoreState LoadState()
    {
        if (state is not null)
        {
            return state;
        }
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetEventStore();
            state = EventStoreSession.LoadStateAsync(store).AsTask().GetAwaiter().GetResult();
            return state;
        }
        finally
        {
            locker.Set();
        }
    }

    public EventStoreSession GetReadOnlySession()
    {
        var store = storageProvider.GetEventStore();
        var state = LoadState();
        return new EventStoreSession(store, state, isReadOnly: true);
    }

    public (EventStoreSession session, EventStoreWriteLock writeLock) GetSessionWithWriteLock()
    {
        var store = storageProvider.GetEventStore();
        var state = LoadState();
        var session = new EventStoreSession(store, state, isReadOnly: false);
        locker.WaitOne();
        var writeLock = new EventStoreWriteLock(
            store,
            updateState: newState => this.state = newState,
            releaseLock: () => locker.Set());
        return (session, writeLock);
    }
}
