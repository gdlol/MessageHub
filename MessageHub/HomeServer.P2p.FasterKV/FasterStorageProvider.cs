using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.FasterKV;

public sealed class FasterStorageProvider : IStorageProvider
{
    private readonly FasterStorageConfig config;
    private readonly ConcurrentDictionary<string, KeyValueStore> stores = new();

    public FasterStorageProvider(FasterStorageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Directory.CreateDirectory(config.DataPath);
        this.config = config;
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (!isDisposed)
        {
            foreach (var store in stores.Values)
            {
                store.Dispose();
            }
            stores.Clear();
            isDisposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(FasterStorageProvider));
        }
    }

    public bool HasKeyValueStore(string name)
    {
        ThrowIfDisposed();

        return File.Exists(Path.Combine(config.DataPath, name));
    }

    public IKeyValueStore GetKeyValueStore(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(nameof(string.IsNullOrEmpty), nameof(name));
        }
        ThrowIfDisposed();

        var store = stores.GetOrAdd(name, _ => new KeyValueStore(name, config));
        return store.CreateSession();
    }
}
