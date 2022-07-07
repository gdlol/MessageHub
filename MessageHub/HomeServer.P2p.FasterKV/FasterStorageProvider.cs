using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.FasterKV;

public class FasterStorageConfig
{
    public string DataPath { get; set; } = default!;
}

public sealed class FasterStorageProvider : IStorageProvider
{
    private readonly string dataPath;
    private readonly ConcurrentDictionary<string, KeyValueStore> stores;

    public FasterStorageProvider(FasterStorageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        dataPath = Path.Combine(config.DataPath, nameof(FasterKV));
        Directory.CreateDirectory(dataPath);
        stores = new ConcurrentDictionary<string, KeyValueStore>();
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

        return File.Exists(Path.Combine(dataPath, name));
    }

    public IKeyValueStore GetKeyValueStore(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(nameof(string.IsNullOrEmpty), nameof(name));
        }
        ThrowIfDisposed();

        var path = Path.Combine(dataPath, name);
        var store = stores.GetOrAdd(name, _ => new KeyValueStore(path));
        return store.CreateSession();
    }
}
