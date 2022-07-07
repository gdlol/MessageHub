using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Rooms;

internal class EventStoreWriteLock : IDisposable
{
    private readonly IKeyValueStore store;
    private readonly Action<EventStoreState> updateState;
    private Action? releaseLock;

    public EventStoreWriteLock(IKeyValueStore store, Action<EventStoreState> updateState, Action releaseLock)
    {
        this.store = store;
        this.updateState = updateState;
        this.releaseLock = releaseLock;
    }

    private void Release()
    {
        if (releaseLock is not null)
        {
            releaseLock();
            releaseLock = null;
        }
    }

    public void Dispose() => Release();

    public async ValueTask CommitAndReleaseAsync(EventStoreState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        await store.CommitAsync();
        updateState(newState);
        Release();
    }
}
