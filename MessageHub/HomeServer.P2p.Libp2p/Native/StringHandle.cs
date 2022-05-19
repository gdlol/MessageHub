using System.Runtime.InteropServices;
using System.Text;

namespace MessageHub.HomeServer.P2p.Libp2p.Native;

internal sealed class StringHandle : SafeHandle
{
    public StringHandle()
        : base(IntPtr.Zero, true)
    { }

    private StringHandle(IntPtr ptr)
        : base(IntPtr.Zero, true)
    {
        handle = ptr;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Marshal.FreeHGlobal(handle);
        return true;
    }

    public override string? ToString()
    {
        return Marshal.PtrToStringUTF8(handle);
    }

    public unsafe static StringHandle FromString(string s)
    {
        int length = Encoding.UTF8.GetMaxByteCount(s.Length) + 1;
        var ptr = Marshal.AllocHGlobal(length);
        var span = new Span<byte>(ptr.ToPointer(), length);
        Encoding.UTF8.GetBytes(s, span);
        span[length - 1] = 0;
        return new StringHandle(ptr);
    }

    public unsafe static StringHandle FromUtf8Bytes(Span<byte> utf8Bytes)
    {
        int length = utf8Bytes.Length + 1;
        var ptr = Marshal.AllocHGlobal(length);
        var span = new Span<byte>(ptr.ToPointer(), length);
        utf8Bytes.CopyTo(span);
        span[length - 1] = 0;
        return new StringHandle(ptr);
    }
}
