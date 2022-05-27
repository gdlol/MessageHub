using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Faster;

public class FasterStorageConfig
{
    public string DataPath { get; set; } = default!;
}

public class FasterStorageProvider : IStorageProvider
{
    private readonly FasterStorageConfig config;

    public FasterStorageProvider(FasterStorageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        this.config = config;
    }

    public bool HasKeyValueStore(string name)
    {
        return File.Exists(Path.Combine(config.DataPath, name));
    }

    public IKeyValueStore GetKeyValueStore(string name)
    {
        var path = Path.Combine(config.DataPath, name);
        return new KeyValueStore(path);
    }
}
