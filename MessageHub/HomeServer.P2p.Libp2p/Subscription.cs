using System.Text.Json;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Subscription : IDisposable
{
    private readonly SubscriptionHandle handle;

    public string Topic { get; }

    internal SubscriptionHandle Handle => handle;

    internal Subscription(SubscriptionHandle handle, string topic)
    {
        this.handle = handle;
        Topic = topic;
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Cancel()
    {
        using var error = NativeMethods.CancelSubscription(handle);
        LibP2pException.Check(error);
    }

    public (string, JsonElement) Next(CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        using var error = NativeMethods.GetNextMessage(context.Handle, handle, out var senderID, out var messageJSON);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
        using var id = senderID;
        using var message = messageJSON;
        return (id.ToString(), JsonSerializer.Deserialize<JsonElement>(message.ToString()));
    }
}
