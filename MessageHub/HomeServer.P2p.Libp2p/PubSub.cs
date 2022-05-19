using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class PubSub : IDisposable
{
    private readonly PubSubHandle handle;

    internal PubSubHandle Handle => handle;

    private PubSub(PubSubHandle handle)
    {
        this.handle = handle;
    }

    public static PubSub Create(DHT dht, CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        var error = NativeMethods.CreatePubSub(context.Handle, dht.Handle, out var pubsubHandle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        return new PubSub(pubsubHandle);
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public Topic JoinTopic(string topic)
    {
        using var topicString = StringHandle.FromString(topic);
        var error = NativeMethods.JoinTopic(handle, topicString, out var topicHandle);
        LibP2pException.Check(error);
        return new Topic(topicHandle);
    }
}
