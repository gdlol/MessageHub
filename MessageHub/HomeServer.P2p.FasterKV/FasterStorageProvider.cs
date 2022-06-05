using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.FasterKV;

public class FasterStorageConfig
{
    public string DataPath { get; set; } = default!;
}

public class FasterStorageProvider : IStorageProvider
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

    public bool HasKeyValueStore(string name)
    {
        return File.Exists(Path.Combine(dataPath, name));
    }

    public IKeyValueStore GetKeyValueStore(string name)
    {
        var path = Path.Combine(dataPath, name);
        var store = stores.GetOrAdd(name, _ => new KeyValueStore(path));
        return store.CreateSession();
    }
}
