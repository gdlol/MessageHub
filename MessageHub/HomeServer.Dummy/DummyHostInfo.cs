namespace MessageHub.HomeServer.Dummy;

public class DummyHostInfo
{
    public string ServerName => "dummy";

    public static DummyHostInfo Instance { get; } = new();
}
