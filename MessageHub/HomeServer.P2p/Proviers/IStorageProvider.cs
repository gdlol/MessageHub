using System.Text;

namespace MessageHub.HomeServer.P2p.Providers;

public interface IKeyValueIterator : IDisposable
{
    public (string, ReadOnlyMemory<byte>) CurrentValue { get; }
    ValueTask<bool> TryMoveAsync();
}

public interface IKeyValueStore : IDisposable
{
    bool IsEmpty { get; }
    ValueTask PutAsync(string key, ReadOnlyMemory<byte> value);
    ValueTask<byte[]?> GetAsync(string key);
    ValueTask CommitAsync();
    IKeyValueIterator Iterate();

    public async ValueTask<string?> GetStringAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var bytes = await GetAsync(key);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    public async ValueTask PutStringAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        await PutAsync(key, Encoding.UTF8.GetBytes(value));
        await CommitAsync();
    }
}

public interface ILogIterator : IDisposable
{
    public ReadOnlyMemory<byte> CurrentValue { get; }
    ValueTask<bool> TryMoveForwardAsync();
    ValueTask<bool> TryMoveBackwardAsync();
}

public interface ILogStore : IDisposable
{
    bool IsEmpty { get; }
    ILogIterator GetLogIterator();
}

public interface IStorageProvider
{
    IKeyValueStore GetKeyValueStore(string name);
    ILogStore GetLogStore(string name);
}
