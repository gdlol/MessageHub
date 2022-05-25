using System.Text.Json;
using MessageHub.HomeServer.P2p.Libp2p.Native;

namespace MessageHub.HomeServer.P2p.Libp2p;

public sealed class MemberStore : IDisposable
{
    private readonly MemberStoreHandle handle;

    internal MemberStoreHandle Handle => handle;

    public MemberStore()
    {
        handle = NativeMethods.CreateMemberStore();
    }

    public string[] GetMembers(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using var topicString = StringHandle.FromString(topic);
        using var error = NativeMethods.GetMembers(handle, topicString, out var resultHandle);
        LibP2pException.Check(error);
        using var _ = resultHandle;
        var result = JsonSerializer.Deserialize<string[]>(resultHandle.ToString());
        if (result is null)
        {
            throw new InvalidOperationException();
        }
        return result;
    }

    public void ClearMembers(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using var topicString = StringHandle.FromString(topic);
        NativeMethods.ClearMembers(handle, topicString);
    }

    public void AddMember(string topic, string peerId)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(peerId);

        using var topicString = StringHandle.FromString(topic);
        using var peerIdString = StringHandle.FromString(peerId);
        NativeMethods.AddMember(handle, topicString, peerIdString);
    }

    public void RemoveMember(string topic, string peerId)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(peerId);

        using var topicString = StringHandle.FromString(topic);
        using var peerIdString = StringHandle.FromString(peerId);
        NativeMethods.RemoveMember(handle, topicString, peerIdString);
    }

    public void Dispose()
    {
        handle.Dispose();
    }
}
