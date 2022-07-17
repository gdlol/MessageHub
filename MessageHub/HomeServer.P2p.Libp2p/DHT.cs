using MessageHub.HomeServer.P2p.Libp2p.Native;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class DHTConfig
{
    public string[]? BootstrapPeers { get; init; }
}

public sealed class DHT : IDisposable
{
    private readonly DHTHandle handle;

    internal DHTHandle Handle => handle;

    public Host Host { get; }

    private DHT(DHTHandle handle, Host host)
    {
        this.handle = handle;
        Host = host;
    }

    public static DHT Create(Host host, DHTConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(config);

        using var context = new Context(cancellationToken);
        using var configJson = StringHandle.FromUtf8Bytes(DefaultJsonSerializer.SerializeToUtf8Bytes(config));
        using var error = NativeMethods.CreateDHT(context.Handle, host.Handle, configJson, out var dhtHandle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        return new DHT(dhtHandle, host);
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (!isDisposed)
        {
            handle.Dispose();
            isDisposed = true;
        }
    }

    public void Bootstrap(CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        using var error = NativeMethods.BootstrapDHT(context.Handle, handle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public string? FindPeer(string peerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peerId);

        using var context = new Context(cancellationToken);
        using var peerIdString = StringHandle.FromString(peerId);
        using var error = NativeMethods.FindPeer(context.Handle, handle, peerIdString, out var resultJSON);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        if (resultJSON.IsInvalid)
        {
            return null;
        }
        using var _ = resultJSON;
        return resultJSON.ToString();
    }

    internal string? FeedClosestPeersToAutoRelay(CancellationToken cancellationToken)
    {
        using var context = new Context(cancellationToken);
        using var error = NativeMethods.FeedClosestPeersToAutoRelay(context.Handle, Host.Handle, handle);
        if (error.IsInvalid)
        {
            return null;
        }
        return error.ToString();
    }
}
