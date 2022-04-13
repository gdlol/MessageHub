namespace MessageHub.HomeServer.Dummy;

public class DummyHostInfo : IHostInfo
{
    public string ServerName => "dummy";

    public static DummyHostInfo Instance { get; } = new();
}
