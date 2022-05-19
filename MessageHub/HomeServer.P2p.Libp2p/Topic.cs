using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Topic : IDisposable
{
    private readonly TopicHandle handle;
    
    internal TopicHandle Handle => handle;

    internal Topic(TopicHandle handle)
    {
        this.handle = handle;
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Close()
    {
        var error = NativeMethods.CloseTopic(handle);
        LibP2pException.Check(error);
    }
}
