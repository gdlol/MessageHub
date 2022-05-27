using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.HomeServer.P2p;

public class UserProfile : IUserProfile
{
    private const string storeName = nameof(UserProfile);
    private const string avatarUrlKey = "avatarUrl";
    private const string displayNameKey = "displayName";

    private readonly IStorageProvider storageProvider;

    public UserProfile(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.storageProvider = storageProvider;
    }

    private async Task<string?> GetStringAsync(string key)
    {
        using var store = storageProvider.GetKeyValueStore(storeName);
        return await store.GetStringAsync(key);
    }

    private async Task PutStringAsync(string key, string value)
    {
        using var store = storageProvider.GetKeyValueStore(storeName);
        await store.PutStringAsync(key, value);
        await store.CommitAsync();
    }

    public Task<string?> GetAvatarUrlAsync(string userId)
    {
        return GetStringAsync(avatarUrlKey);
    }

    public Task<string?> GetDisplayNameAsync(string userId)
    {
        return GetStringAsync(displayNameKey);
    }

    public Task SetAvatarUrlAsync(string userId, string url)
    {
        return PutStringAsync(avatarUrlKey, url);
    }

    public Task SetDisplayNameAsync(string userId, string name)
    {
        return PutStringAsync(displayNameKey, name);
    }
}
