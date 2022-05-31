using Microsoft.Extensions.Caching.Memory;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class AddressCache : IMemoryCache
{
    private readonly IMemoryCache cache;

    public AddressCache(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public ICacheEntry CreateEntry(object key)
    {
        return cache.CreateEntry(key);
    }

    public void Dispose()
    {
        cache.Dispose();
    }

    public void Remove(object key)
    {
        cache.Remove(key);
    }

    public bool TryGetValue(object key, out object value)
    {
        return cache.TryGetValue(key, out value);
    }
}
