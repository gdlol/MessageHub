using System.Collections.Concurrent;
using System.Text;
using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.Complement.HomeServer;

public sealed class UserLogIn : IUserLogIn, IDisposable
{
    private const string storeName = nameof(UserLogIn);

    private readonly ManualResetEvent locker = new(initialState: true);
    private readonly ConcurrentDictionary<string, string> userNameMapping = new();
    private bool isInitialized = false;

    private readonly IStorageProvider storageProvider;
    private readonly HomeServerClient homeServerClient;
    private readonly IUserRegistration userRegistration;

    public UserLogIn(
        IStorageProvider storageProvider,
        HomeServerClient homeServerClient,
        IUserRegistration userRegistration)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(homeServerClient);
        ArgumentNullException.ThrowIfNull(userRegistration);

        this.storageProvider = storageProvider;
        this.homeServerClient = homeServerClient;
        this.userRegistration = userRegistration;
    }

    public void Dispose()
    {
        locker.Dispose();
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
                        var (accessToken, userNameBytes) = iterator.CurrentValue;
                        userNameMapping[accessToken] = Encoding.UTF8.GetString(userNameBytes.Span);
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

    public async Task<LogInResponse> LogInAsync(string userName, string? deviceId, string? deviceName)
    {
        await InitializeAsync();

        string? address = await userRegistration.TryGetAddressAsync(userName);
        if (address is null)
        {
            throw new InvalidOperationException($"{nameof(userName)}: {userName}");
        }
        var loginResponse = await homeServerClient.LogInAsync(address, deviceId, deviceName);
        if (loginResponse.AccessToken is null)
        {
            throw new InvalidOperationException();
        }

        using var store = storageProvider.GetKeyValueStore(storeName);
        await store.PutStringAsync(loginResponse.AccessToken, userName);
        await store.CommitAsync();
        userNameMapping[loginResponse.AccessToken] = userName;
        return loginResponse;
    }

    public async Task<string?> TryGetDeviceIdAsync(string accessToken)
    {
        await InitializeAsync();

        if (userNameMapping.TryGetValue(accessToken, out string? userName))
        {
            string? address = await userRegistration.TryGetAddressAsync(userName);
            if (address is null)
            {
                throw new InvalidOperationException($"{nameof(userName)}: {userName}");
            }
            var whoAmI = await homeServerClient.WhoAmIAsync(address, accessToken);
            if (whoAmI is null)
            {
                userNameMapping.TryRemove(accessToken, out string? _);
            }
            else
            {
                return whoAmI.DeviceId;
            }
        }
        return null;
    }

    public async Task<string?> TryGetUserNameAsync(string accessToken)
    {
        await InitializeAsync();

        if (userNameMapping.TryGetValue(accessToken, out string? userName))
        {
            string? deviceId = await TryGetDeviceIdAsync(accessToken);
            if (deviceId is not null) // logged out
            {
                return userName;
            }
        }
        return null;
    }
}
