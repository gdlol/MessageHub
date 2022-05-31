using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class MdnsService : IDisposable
{
    private readonly MdnsServiceHandle handle;

    internal MdnsServiceHandle Handle => handle;

    private MdnsService(MdnsServiceHandle handle)
    {
        this.handle = handle;
    }

    public static MdnsService Create(Host host, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(serviceName);

        using var serviceNameString = StringHandle.FromString(serviceName);
        var handle = NativeMethods.CreateMdnsService(host.Handle, serviceNameString);
        return new MdnsService(handle);
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

    public void Start()
    {
        using var error = NativeMethods.StartMdnsService(handle);
        LibP2pException.Check(error);
    }

    public void Stop()
    {
        using var error = NativeMethods.StopMdnsService(handle);
        LibP2pException.Check(error);
    }
}
