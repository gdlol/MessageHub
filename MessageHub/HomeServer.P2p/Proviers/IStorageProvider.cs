using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHub.HomeServer.P2p.Providers;

public interface IKeyValueIterator : IDisposable
{
    public (string, ReadOnlyMemory<byte>) CurrentValue { get; }
    ValueTask<bool> TryMoveAsync();
}

public interface IKeyValueStore : IDisposable
{
    bool IsEmpty { get; }
    ValueTask PutAsync(string key, ReadOnlyMemory<byte> value);
    ValueTask<byte[]?> GetAsync(string key);
    ValueTask DeleteAsync(string key);
    ValueTask CommitAsync();
    IKeyValueIterator Iterate();

    public async ValueTask<string?> GetStringAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var bytes = await GetAsync(key);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    public async ValueTask PutStringAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        await PutAsync(key, Encoding.UTF8.GetBytes(value));
    }

    public async IAsyncEnumerable<(string key, byte[] value)> GetAsyncEnumerable()
    {
        using var iterator = Iterate();
        do
        {
            var (key, value) = iterator.CurrentValue;
            yield return (key, value.ToArray());
        } while (await iterator.TryMoveAsync());
    }

    public async ValueTask<T?> GetDeserializedValueAsync<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var bytes = await GetAsync(key);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    public async ValueTask PutSerializedValueAsync<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);

        await PutAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }));
    }
}

public interface IStorageProvider
{
    bool HasKeyValueStore(string name);
    IKeyValueStore GetKeyValueStore(string name);
    IKeyValueStore GetEventStore();
}
