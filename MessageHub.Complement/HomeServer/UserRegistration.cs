using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using MessageHub.Complement.HomeServer.P2p.LocalIdentity;
using MessageHub.Complement.Logging;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using MessageHub.HomeServer.P2p.Providers;

namespace MessageHub.Complement.HomeServer;

public sealed class UserRegistration : IUserRegistration, IDisposable
{
    private const string storeName = nameof(UserRegistration);

    private readonly ManualResetEvent locker = new(initialState: true);
    private readonly ConcurrentDictionary<string, IPAddress> serverAddresses = new();
    private bool isInitialized = false;
    private ushort serverCount = 0;

    private readonly ILogger logger;
    private readonly IStorageProvider storageProvider;
    private readonly Config config;
    private readonly HomeServerClient homeServerClient;

    private readonly ConcurrentDictionary<string, string> userIds = new();

    public UserRegistration(
        ILogger<UserRegistration> logger,
        IStorageProvider storageProvider,
        Config config,
        HomeServerClient homeServerClient)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(homeServerClient);

        this.logger = logger;
        this.storageProvider = storageProvider;
        this.config = config;
        this.homeServerClient = homeServerClient;
    }

    public void Dispose()
    {
        locker.Dispose();
    }

    private void StartServer(string userName, string address)
    {
        logger.LogDebug("Starting p2p server for user {}...", userName);

        string applicationPath = Path.Combine(config.DataPath, "Servers", address);
        Directory.CreateDirectory(applicationPath);
        var p2pConfig = new MessageHub.HomeServer.P2p.Config
        {
            ListenAddress = $"{address}:80",
            PrivateNetworkSecret = config.PrivateNetworkSecret,
            LoggerProvider = new PrefixedLoggerProvider(config.ConsoleLoggerProvider, $"{userName}: "),
            Configure = services =>
            {
                services.AddSingleton<IAuthenticator, PersistentLocalAuthenticator>();
            }
        };
        Program.RunAsync(applicationPath, p2pConfig);
    }

    private async ValueTask<ImmutableDictionary<string, IPAddress>> RestoreAsync()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, IPAddress>();
        using var store = storageProvider.GetKeyValueStore(storeName);
        if (!store.IsEmpty)
        {
            using var iterator = store.Iterate();
            do
            {
                var (userName, addressBytes) = iterator.CurrentValue;
                var address = new IPAddress(addressBytes.Span);
                builder.Add(userName, address);
                StartServer(userName, address.ToString());
            } while (await iterator.TryMoveAsync());
        }
        return builder.ToImmutable();
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
                var addresses = await RestoreAsync();
                foreach (var (key, address) in addresses)
                {
                    serverAddresses[key] = address;
                }
                checked { serverCount = (ushort)serverAddresses.Count; }
                isInitialized = true;
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public async ValueTask<bool> TryRegisterAsync(string userName)
    {
        await InitializeAsync();

        bool created = false;
        var serverAddress = serverAddresses.AddOrUpdate(
            userName,
            _ =>
            {
                serverCount++;
                if (serverCount == ushort.MaxValue)
                {
                    throw new InvalidOperationException($"{nameof(serverCount)}: {serverCount}");
                }
                created = true;
                var bytes = IPAddress.Loopback.GetAddressBytes();
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1, 2), serverCount);
                return new IPAddress(bytes);
            },
            (_, address) => address);
        if (created)
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            await store.PutAsync(userName, serverAddress.GetAddressBytes());
            await store.CommitAsync();
            StartServer(userName, serverAddress.ToString());
        }
        return created;
    }

    public async ValueTask<string?> TryGetAddressAsync(string userName)
    {
        await InitializeAsync();

        if (serverAddresses.TryGetValue(userName, out var serverAddress))
        {
            return serverAddress.ToString();
        }
        return null;
    }

    public async ValueTask<string?> TryGetP2pUserIdAsync(string userName)
    {
        await InitializeAsync();

        if (userIds.TryGetValue(userName, out string? value))
        {
            return value;
        }
        string? serverAddress = await TryGetAddressAsync(userName);
        if (serverAddress is null)
        {
            return null;
        }
        var loginResponse = await homeServerClient.LogInAsync(serverAddress);
        await homeServerClient.LogOutAsync(serverAddress, loginResponse.AccessToken!);
        string userId = loginResponse.UserId;
        userIds[userName] = userId;
        return userId;
    }
}
