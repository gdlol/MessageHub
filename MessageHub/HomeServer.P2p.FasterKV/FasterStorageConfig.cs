namespace MessageHub.HomeServer.P2p.FasterKV;

public class FasterStorageConfig
{
    public string DataPath { get; init; } = default!;
    public long? PageSize { get; init; }
    public int? PageCount { get; init; }
}
