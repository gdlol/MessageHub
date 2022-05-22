using System.Text.Json;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class Topic : IDisposable
{
    private readonly TopicHandle handle;

    internal TopicHandle Handle => handle;

    public string Name { get; }

    internal Topic(TopicHandle handle, string name)
    {
        this.handle = handle;
        Name = name;
    }

    public void Dispose()
    {
        handle.Dispose();
    }

    public void Close()
    {
        using var error = NativeMethods.CloseTopic(handle);
        LibP2pException.Check(error);
    }

    public void Publish(JsonElement message, CancellationToken cancellationToken = default)
    {
        using var context = new Context(cancellationToken);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        using var jsonString = StringHandle.FromUtf8Bytes(jsonBytes);
        using var error = NativeMethods.PublishMessage(context.Handle, handle, jsonString);
        if (!error.IsInvalid)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LibP2pException.Check(error);
        }
    }

    public Subscription Subscribe()
    {
        using var error = NativeMethods.Subscribe(handle, out var subscriptionHandle);
        LibP2pException.Check(error);
        return new Subscription(subscriptionHandle);
    }
}
