using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Complement.HomeServer.P2p.LocalIdentity;
using MessageHub.Complement.Logging;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using MessageHub.HomeServer.P2p.Providers;
using Microsoft.AspNetCore.Identity;

namespace MessageHub.Complement.HomeServer;

public sealed class UserRegistration : IUserRegistration, IDisposable
{
    private class UserInfo
    {
        [JsonPropertyName("serverAddress")]
        public string ServerAddress { get; init; } = default!;

        [JsonPropertyName("passwordHash")]
        public string PasswordHash { get; init; } = default!;
    }

    private const string storeName = nameof(UserRegistration);

    private readonly ManualResetEvent locker = new(initialState: true);
    private readonly ConcurrentDictionary<string, UserInfo> userInfoCache = new();
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
            FasterKVPageSize = 4096,
            FasterKVPageCount = 16,
            LoggerProvider = new PrefixedLoggerProvider(config.ConsoleLoggerProvider, $"{userName}: "),
            Configure = services =>
            {
                services.AddSingleton<IAuthenticator, PersistentLocalAuthenticator>();
            }
        };
        Program.RunAsync(applicationPath, p2pConfig);
    }

    private async ValueTask<ImmutableDictionary<string, UserInfo>> RestoreAsync()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, UserInfo>();
        using var store = storageProvider.GetKeyValueStore(storeName);
        if (!store.IsEmpty)
        {
            using var iterator = store.Iterate();
            do
            {
                var (userName, userInfoBytes) = iterator.CurrentValue;
                var userInfo = JsonSerializer.Deserialize<UserInfo>(userInfoBytes.Span)!;
                builder.Add(userName, userInfo);
                StartServer(userName, userInfo.ServerAddress);
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
                var cache = await RestoreAsync();
                foreach (var (key, userInfo) in cache)
                {
                    userInfoCache[key] = userInfo;
                }
                checked { serverCount = (ushort)userInfoCache.Count; }
                isInitialized = true;
            }
        }
        finally
        {
            locker.Set();
        }
    }

    public async ValueTask<bool> TryRegisterAsync(string userName, string password)
    {
        await InitializeAsync();

        bool created = false;
        var passwrodHasher = new PasswordHasher<string>();
        var userInfo = userInfoCache.AddOrUpdate(
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
                string passwordHadh = passwrodHasher.HashPassword(userName, password);
                return new UserInfo
                {
                    ServerAddress = new IPAddress(bytes).ToString(),
                    PasswordHash = passwordHadh
                };
            },
            (_, userInfo) => userInfo);
        if (created)
        {
            using var store = storageProvider.GetKeyValueStore(storeName);
            await store.PutSerializedValueAsync(userName, userInfo);
            await store.CommitAsync();
            StartServer(userName, userInfo.ServerAddress);
        }
        return created;
    }

    public async ValueTask<bool> VerifyUserAsync(string userName, string password)
    {
        await InitializeAsync();

        if (userInfoCache.TryGetValue(userName, out var userInfo))
        {
            var passwrodHasher = new PasswordHasher<string>();
            var result = passwrodHasher.VerifyHashedPassword(userName, userInfo.PasswordHash, password);
            return result == PasswordVerificationResult.Success;
        }
        return false;
    }

    public async ValueTask<string?> TryGetAddressAsync(string userName)
    {
        await InitializeAsync();

        if (userInfoCache.TryGetValue(userName, out var userInfo))
        {
            return userInfo.ServerAddress;
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
