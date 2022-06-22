using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Discovery : IDisposable
{
    private readonly DiscoveryHandle handle;

    internal DiscoveryHandle Handle => handle;

    private Discovery(DiscoveryHandle handle)
    {
        this.handle = handle;
    }

    public static Discovery Create(DHT dht)
    {
        ArgumentNullException.ThrowIfNull(dht);

        var handle = NativeMethods.CreateDiscovery(dht.Handle);
        return new Discovery(handle);
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Advertise(string topic, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using var context = new Context(cancellationToken);
        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.Advertise(context.Handle, handle, topicString, checked((int)ttl.TotalSeconds));
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public IEnumerable<(string peerId, string addressInfo)> FindPeers(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using var context = new Context(cancellationToken);
        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.FindPeers(context.Handle, handle, topicString, out var resultHandle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        using var peerChannel = new PeerChannel(resultHandle);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (peerChannel.TryGetNextPeer(context, out string? addressInfo))
            {
                string peerId = Host.GetIdFromAddressInfo(addressInfo);
                if (Host.IsValidAddressInfo(addressInfo))
                {
                    yield return (peerId, addressInfo);
                }
            }
            else
            {
                break;
            }
        }
    }
}
