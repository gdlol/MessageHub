using System.Diagnostics.CodeAnalysis;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal sealed class PeerChannel : IDisposable
{
    private readonly PeerChanHandle handle;

    internal PeerChanHandle Handle => handle;

    public PeerChannel(PeerChanHandle handle)
    {
        this.handle = handle;
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public bool TryGetNextPeer(Context context, [NotNullWhen(true)] out string? addressInfo)
    {
        addressInfo = null;
        using var error = NativeMethods.TryGetNextPeer(context.Handle, handle, out var resultJson);
        LibP2pException.Check(error);
        if (resultJson.IsInvalid)
        {
            return false;
        }
        using var _ = resultJson;
        addressInfo = resultJson.ToString();
        return true;
    }
}
