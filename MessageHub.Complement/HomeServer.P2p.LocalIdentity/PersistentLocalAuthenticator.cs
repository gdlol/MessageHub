using System.Collections.Concurrent;
using System.Text;
using MessageHub.HomeServer;
using MessageHub.HomeServer.P2p.LocalIdentity;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.Complement.HomeServer.P2p.LocalIdentity;

public class PersistentLocalAuthenticator : IAuthenticator
{
    private const string storeName = "AccessTokens";

    private readonly ManualResetEvent locker = new(initialState: true);
    private bool isInitialized = false;
    private readonly ConcurrentDictionary<string, string> tokenMapping = new();

    private readonly IAuthenticator authenticator;
    private readonly IStorageProvider storageProvider;

    public PersistentLocalAuthenticator(LocalAuthenticator authenticator, IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(storageProvider);

        this.authenticator = authenticator;
        this.storageProvider = storageProvider;
    }

    private async ValueTask InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }
        locker.WaitOne();
        try
        {
            if (!isInitialized)
            {
                using var store = storageProvider.GetKeyValueStore(storeName);
                if (!store.IsEmpty)
                {
                    using var iterator = store.Iterate();
                    do
                    {
                        var (deviceId, savedToken) = iterator.CurrentValue;
                        var result = await authenticator.LogInAsync(deviceId, "loginToken");
                        if (result is null)
                        {
                            throw new InvalidOperationException();
                        }
                        tokenMapping[Encoding.UTF8.GetString(savedToken.Span)] = result.Value.accessToken;
                    } while (await iterator.TryMoveAsync());
                }

                isInitialized = true;
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public async Task<string?> AuthenticateAsync(string accessToken)
    {
        await InitializeAsync();
        if (tokenMapping.TryGetValue(accessToken, out string? value))
        {
            accessToken = value;
        }
        return await authenticator.AuthenticateAsync(accessToken);
    }

    public async Task<string?> GetDeviceIdAsync(string accessToken)
    {
        await InitializeAsync();
        if (tokenMapping.TryGetValue(accessToken, out string? value))
        {
            accessToken = value;
        }

        return await authenticator.GetDeviceIdAsync(accessToken);
    }

    public async Task<string[]> GetDeviceIdsAsync()
    {
        await InitializeAsync();

        return await authenticator.GetDeviceIdsAsync();
    }

    public string GetSsoRedirectUrl(string redirectUrl)
    {
        return authenticator.GetSsoRedirectUrl(redirectUrl);
    }

    public async Task<(string userId, string accessToken)?> LogInAsync(string deviceId, string token)
    {
        await InitializeAsync();

        var result = await authenticator.LogInAsync(deviceId, token);
        if (result is not null)
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            await store.PutStringAsync(deviceId, result.Value.accessToken);
            await store.CommitAsync();
        }
        return result;
    }

    public async Task LogOutAllAsync()
    {
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            if (!store.IsEmpty)
            {
                using var iterator = store.Iterate();
                do
                {
                    var (deviceId, _) = iterator.CurrentValue;
                    await store.DeleteAsync(deviceId);
                } while (await iterator.TryMoveAsync());
            }
            await store.CommitAsync();
            tokenMapping.Clear();
            await authenticator.LogOutAllAsync();
        }
        finally
        {
            locker.Set();
        }
    }

    public async Task<int> LogOutAsync(string deviceId)
    {
        locker.WaitOne();
        try
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            string? savedToken = await store.GetStringAsync(deviceId);
            if (savedToken is not null)
            {
                await store.DeleteAsync(deviceId);
                await store.CommitAsync();
                tokenMapping.TryRemove(savedToken, out var _);
            }
            return await authenticator.LogOutAsync(deviceId);
        }
        finally
        {
            locker.Set();
        }
    }
}
