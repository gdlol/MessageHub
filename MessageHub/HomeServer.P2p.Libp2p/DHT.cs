using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public class DHTConfig
{
    public string[]? BootstrapPeers { get; init; }
}

public sealed class DHT : IDisposable
{
    private readonly DHTHandle handle;

    internal DHTHandle Handle => handle;

    private DHT(DHTHandle handle)
    {
        this.handle = handle;
    }

    public static DHT Create(Host host, DHTConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(config);

        using var context = new Context(cancellationToken);
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        using var configJson = StringHandle.FromUtf8Bytes(JsonSerializer.SerializeToUtf8Bytes(config, options));
        using var error = NativeMethods.CreateDHT(context.Handle, host.Handle, configJson, out var dhtHandle);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        return new DHT(dhtHandle);
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
}
