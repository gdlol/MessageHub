using System.Text;
using FASTER.core;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.Faster;

internal class KeyValueIterator : IKeyValueIterator
{
    private readonly IFasterScanIterator<ReadOnlyMemory<byte>, Memory<byte>> iterator;

    public KeyValueIterator(IFasterScanIterator<ReadOnlyMemory<byte>, Memory<byte>> iterator)
    {
        ArgumentNullException.ThrowIfNull(iterator);

        this.iterator = iterator;
        iterator.GetValue();
    }

    public (string, ReadOnlyMemory<byte>) CurrentValue
    {
        get
        {
            var key = iterator.GetKey();
            var value = iterator.GetValue();
            return (Encoding.UTF8.GetString(key.Span), value);
        }
    }

    public void Dispose()
    {
        iterator.Dispose();
    }

    public ValueTask<bool> TryMoveAsync()
    {
        return ValueTask.FromResult(iterator.GetNext(out var _));
    }
}
