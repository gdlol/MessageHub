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
        Native.Free(handle);
        return true;
    }

    public override string ToString()
    {
        return Marshal.PtrToStringUTF8(handle) ?? string.Empty;
    }

    public unsafe static StringHandle FromString(string s)
    {
        int maxLength = Encoding.UTF8.GetMaxByteCount(s.Length) + 1;
        var ptr = Native.Alloc(maxLength);
        var span = new Span<byte>(ptr.ToPointer(), maxLength);
        int length = Encoding.UTF8.GetBytes(s, span);
        span[length] = 0;
        return new StringHandle(ptr);
    }

    public unsafe static StringHandle FromUtf8Bytes(Span<byte> utf8Bytes)
    {
        int length = utf8Bytes.Length + 1;
        var ptr = Native.Alloc(length);
        var span = new Span<byte>(ptr.ToPointer(), length);
        utf8Bytes.CopyTo(span);
        span[length - 1] = 0;
        return new StringHandle(ptr);
    }
}
