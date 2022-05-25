using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal sealed class Context : IDisposable
{
    private readonly ContextHandle handle;
    private readonly CancellationTokenRegistration registration;

    internal ContextHandle Handle => handle;

    public Context(CancellationToken cancellationToken)
    {
        handle = NativeMethods.CreateContext();
        registration = cancellationToken.Register(() => NativeMethods.CancelContext(handle));
    }

    public void Dispose()
    {
        registration.Dispose();
        handle.Dispose();
    }
}
