using System.Runtime.InteropServices;

namespace MessageHub.HomeServer.P2p.Libp2p.Native;

internal static class Native
{
    public const string DllName = "messagehub-libp2p.dll";
}

internal class ObjectHandle : SafeHandle
{
    [DllImport(Native.DllName)]
    private static extern void Release(IntPtr handle);

    public ObjectHandle()
        : base(IntPtr.Zero, true)
    { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Release(handle);
        Marshal.FreeHGlobal(handle);
        return true;
    }

    public override string? ToString()
    {
        return Marshal.PtrToStringUTF8(handle);
    }
}

internal sealed class ContextHandle : ObjectHandle { }

internal sealed class HostHandle : ObjectHandle
{
    [DllImport(Native.DllName)]
    private static extern StringHandle CloseHost(IntPtr handle);

    protected override bool ReleaseHandle()
    {
        var errorHandle = CloseHost(handle);
        return base.ReleaseHandle() && errorHandle.IsInvalid;
    }
}

internal sealed class DHTHandle : ObjectHandle
{
    [DllImport(Native.DllName)]
    private static extern StringHandle CloseDHT(IntPtr handle);

    protected override bool ReleaseHandle()
    {
        var errorHandle = CloseDHT(handle);
        return base.ReleaseHandle() && errorHandle.IsInvalid;
    }
}

internal sealed class DiscoveryHandle : ObjectHandle { }

internal sealed class MemberStoreHandle : ObjectHandle { }

internal sealed class PubSubHandle : ObjectHandle { }

internal sealed class TopicHandle : ObjectHandle { }

internal sealed class SubscriptionHandle : ObjectHandle { }

internal unsafe static class NativeMethods
{
    [DllImport(Native.DllName)]
    public static extern ContextHandle CreateContext();

    [DllImport(Native.DllName)]
    public static extern void CancelContext(ContextHandle handle);

    [DllImport(Native.DllName)]
    public static extern StringHandle CreateHost(StringHandle configJSON, out HostHandle handle);

    [DllImport(Native.DllName)]
    public static extern StringHandle GetHostID(HostHandle hostHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle GetHostAddressInfo(HostHandle hostHandle, out StringHandle resultJSON);

    [DllImport(Native.DllName)]
    public static extern StringHandle ConnectHost(
        ContextHandle ctxHandle,
        HostHandle hostHandle,
        StringHandle addrInfo);

    [DllImport(Native.DllName)]
    public static extern StringHandle CreateDHT(
        ContextHandle ctxHandle,
        HostHandle hostHandle,
        StringHandle configJSON,
        out DHTHandle dhtHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle BootstrapDHT(ContextHandle ctxHandle, DHTHandle dhtHandle);

    [DllImport(Native.DllName)]
    public static extern DiscoveryHandle CreateDiscovery(DHTHandle dhtHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle Advertise(
        ContextHandle ctxHandle,
        DiscoveryHandle discoveryHandle,
        StringHandle topic);

    [DllImport(Native.DllName)]
    public static extern StringHandle FindPeers(
        ContextHandle ctxHandle,
        DiscoveryHandle discoveryHandle,
        StringHandle topic,
        out StringHandle resultJSON);

    [DllImport(Native.DllName)]
    public static extern MemberStoreHandle CreateMemberStore();

    [DllImport(Native.DllName)]
    public static extern StringHandle GetMembers(
        MemberStoreHandle memberStoreHandle,
        StringHandle topic,
        out StringHandle resultJSON);

    [DllImport(Native.DllName)]
    public static extern void ClearMembers(MemberStoreHandle memberStoreHandle, StringHandle topic);

    [DllImport(Native.DllName)]
    public static extern void AddMember(MemberStoreHandle memberStoreHandle, StringHandle topic, StringHandle peerID);

    [DllImport(Native.DllName)]
    public static extern void RemoveMember(
        MemberStoreHandle memberStoreHandle,
        StringHandle topic,
        StringHandle peerID);

    [DllImport(Native.DllName)]
    public static extern StringHandle CreatePubSub(
        ContextHandle ctxHandle,
        DHTHandle dhtHandle,
        MemberStoreHandle memberStoreHandle,
        out PubSubHandle pubsubHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle JoinTopic(
        PubSubHandle pubsubHandle,
        StringHandle topic,
        out TopicHandle topicHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle CloseTopic(TopicHandle topicHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle PublishMessage(
        ContextHandle ctxHandle,
        TopicHandle topicHandle,
        StringHandle message);

    [DllImport(Native.DllName)]
    public static extern StringHandle Subscribe(TopicHandle topicHandle, out SubscriptionHandle subscriptionHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle CancelSubscription(SubscriptionHandle subscriptionHandle);

    [DllImport(Native.DllName)]
    public static extern StringHandle GetNextMessage(
        ContextHandle ctxHandle,
        SubscriptionHandle subscriptionHandle,
        out StringHandle senderID,
        out StringHandle messageJSON);
}
