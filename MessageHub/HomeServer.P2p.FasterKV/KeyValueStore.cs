using System.Buffers;
using System.Text;
using FASTER.core;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p.FasterKV;
using ClientSession = ClientSession<
    ReadOnlyMemory<byte>, Memory<byte>, Memory<byte>, (IMemoryOwner<byte>, int), Empty,
    IFunctions<ReadOnlyMemory<byte>, Memory<byte>, Memory<byte>, (IMemoryOwner<byte>, int), Empty>>;

internal class KeyValueSession : IKeyValueStore
{
    private readonly FasterKV<ReadOnlyMemory<byte>, Memory<byte>> fasterKV;
    private readonly ClientSession session;

    public KeyValueSession(FasterKV<ReadOnlyMemory<byte>, Memory<byte>> fasterKV)
    {
        ArgumentNullException.ThrowIfNull(fasterKV);

        this.fasterKV = fasterKV;
        session = fasterKV.NewSession(new MemoryFunctions<ReadOnlyMemory<byte>, byte, Empty>());
    }

    public void Dispose()
    {
        session.Dispose();
    }

    public bool IsEmpty => fasterKV.Log.HeadAddress == fasterKV.Log.TailAddress;

    public async ValueTask CommitAsync()
    {
        await session.CompletePendingAsync();
        await fasterKV.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
    }

    public ValueTask DeleteAsync(string key)
    {
        session.Delete(Encoding.UTF8.GetBytes(key));
        return ValueTask.CompletedTask;
    }

    public async ValueTask<byte[]?> GetAsync(string key)
    {
        var readResult = await session.ReadAsync(Encoding.UTF8.GetBytes(key));
        var (status, output) = readResult.Complete();
        byte[]? result = null;
        if (status.Found)
        {
            var (owner, length) = output;
            using var _ = owner;
            result = owner.Memory[..length].ToArray();
        }
        return result;
    }

    public ValueTask PutAsync(string key, Memory<byte> value)
    {
        ReadOnlyMemory<byte> keyMemory = Encoding.UTF8.GetBytes(key);
        session.Upsert(ref keyMemory, ref value);
        return ValueTask.CompletedTask;
    }

    public IKeyValueIterator Iterate()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException(nameof(IsEmpty));
        }
        var iterator = session.Iterate();
        if (!iterator.GetNext(out var _))
        {
            throw new InvalidOperationException();
        }
        return new KeyValueIterator(iterator);
    }
}

internal class KeyValueStore
{
    private readonly IDevice device;
    private readonly FasterKVSettings<ReadOnlyMemory<byte>, Memory<byte>> settings;
    private readonly FasterKV<ReadOnlyMemory<byte>, Memory<byte>> fasterKV;

    public KeyValueStore(string name, FasterStorageConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var path = Path.Combine(config.DataPath, name);
        device = Devices.CreateLogDevice(path);
        settings = new FasterKVSettings<ReadOnlyMemory<byte>, Memory<byte>>(path)
        {
            LogDevice = device,
            ObjectLogDevice = new NullDevice(),
            TryRecoverLatest = true,
            ReadCacheEnabled = true
        };
        if (config.PageSize is long pageSize)
        {
            settings.PageSize = pageSize;
            settings.ReadCachePageSize = pageSize;
        }
        if (config.PageCount is int pageCount)
        {
            settings.MemorySize = settings.PageSize * pageCount;
            settings.ReadCacheMemorySize = settings.ReadCachePageSize * pageCount;
        }
        fasterKV = new FasterKV<ReadOnlyMemory<byte>, Memory<byte>>(settings);
    }

    public void Dispose()
    {
        fasterKV.Dispose();
        settings.Dispose();
        device.Dispose();
    }

    public IKeyValueStore CreateSession()
    {
        return new KeyValueSession(fasterKV);
    }
}
