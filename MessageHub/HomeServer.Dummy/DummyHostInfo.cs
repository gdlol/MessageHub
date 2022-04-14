namespace MessageHub.HomeServer.Dummy;

public class DummyHostInfo : IHostInfo
{
    public string ServerName => "localhost:8448";

    public static DummyHostInfo Instance { get; } = new();
}
