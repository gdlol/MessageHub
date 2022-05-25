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

    public static PubSub Create(DHT dht, MemberStore memberStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dht);
        ArgumentNullException.ThrowIfNull(memberStore);

        using var context = new Context(cancellationToken);
        using var error = NativeMethods.CreatePubSub(
            context.Handle,
            dht.Handle,
            memberStore.Handle,
            out var pubsubHandle);
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
        ArgumentNullException.ThrowIfNull(topic);

        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.JoinTopic(handle, topicString, out var topicHandle);
        LibP2pException.Check(error);
        return new Topic(topicHandle, topic);
    }
}
