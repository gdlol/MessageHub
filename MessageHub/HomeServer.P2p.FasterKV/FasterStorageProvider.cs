using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.FasterKV;

public class FasterStorageConfig
{
    public string DataPath { get; set; } = default!;
}

public class FasterStorageProvider : IStorageProvider
{
    private readonly FasterStorageConfig config;
    private readonly ConcurrentDictionary<string, KeyValueStore> stores;

    public FasterStorageProvider(FasterStorageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        this.config = config;
        stores = new ConcurrentDictionary<string, KeyValueStore>();
    }

    public bool HasKeyValueStore(string name)
    {
        return File.Exists(Path.Combine(config.DataPath, name));
    }

    public IKeyValueStore GetKeyValueStore(string name)
    {
        var path = Path.Combine(config.DataPath, name);
        var store = stores.GetOrAdd(name, _ => new KeyValueStore(path));
        return store.CreateSession();
    }
}
