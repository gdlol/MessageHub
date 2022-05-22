using System.Text.Json;
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
        var handle = NativeMethods.CreateDiscovery(dht.Handle);
        return new Discovery(handle);
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Advertise(string topic, CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.Advertise(context.Handle, handle, topicString);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public Dictionary<string, string> FindPeers(string topic, CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.FindPeers(context.Handle, handle, topicString, out var resultHandle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        using var _ = resultHandle;
        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(resultHandle.ToString());
        if (result is null)
        {
            throw new InvalidOperationException();
        }
        return result;
    }
}
