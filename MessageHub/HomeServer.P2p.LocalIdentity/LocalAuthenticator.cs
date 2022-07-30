using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Web;
using MessageHub.HomeServer.P2p.Providers;
using NSec.Cryptography;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

public sealed class LocalAuthenticator : IAuthenticator, IDisposable
{
    private readonly ManualResetEvent locker = new(initialState: true);
    private const string KeyStoreName = nameof(LocalIdentity);
    private const string privateKeyName = "private";

    private readonly Config config;
    private readonly IStorageProvider storageProvider;
    private readonly INetworkProvider networkProvider;
    private readonly LocalIdentityService localIdentityService;
    private readonly IUserProfile userProfile;
    private readonly string loginToken = nameof(loginToken);
    private readonly JwtAuthenticator jwtAuthenticator = new();
    private readonly ConcurrentDictionary<string, string> accessTokens = new();

    public LocalAuthenticator(
        Config config,
        IStorageProvider storageProvider,
        INetworkProvider networkProvider,
        LocalIdentityService localIdentityService,
        IUserProfile userProfile)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(networkProvider);
        ArgumentNullException.ThrowIfNull(localIdentityService);
        ArgumentNullException.ThrowIfNull(userProfile);

        this.config = config;
        this.storageProvider = storageProvider;
        this.networkProvider = networkProvider;
        this.localIdentityService = localIdentityService;
        this.userProfile = userProfile;
    }

    public void Dispose()
    {
        jwtAuthenticator.Dispose();
    }

    internal async Task<(bool created, Key key)> CreateOrGetPrivateKeyAsync()
    {
        locker.WaitOne();
        try
        {
            var store = storageProvider.GetKeyValueStore(KeyStoreName);
            var privateKeyBlob = await store.GetAsync(privateKeyName);
            bool created = false;
            if (privateKeyBlob is null)
            {
                using var key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters
                {
                    ExportPolicy = KeyExportPolicies.AllowPlaintextExport
                });
                privateKeyBlob = key.Export(KeyBlobFormat.RawPrivateKey);
                await store.PutAsync(privateKeyName, privateKeyBlob);
                await store.CommitAsync();
                created = true;
            }
            try
            {
                return (created, Key.Import(SignatureAlgorithm.Ed25519, privateKeyBlob, KeyBlobFormat.RawPrivateKey));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateKeyBlob);
            }
        }
        finally
        {
            locker.Set();
        }
    }

    internal LocalIdentity CreateIdentity(Key key)
    {
        var (keyId, networkKey) = networkProvider.GetVerifyKey();
        var localIdentity = LocalIdentity.Create(
            key,
            new VerifyKeys(new Dictionary<KeyIdentifier, string>
            {
                [keyId] = networkKey,
            }.ToImmutableDictionary(), DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()));
        return localIdentity;
    }

    public string GetSsoRedirectUrl(string redirectUrl)
    {
        ArgumentNullException.ThrowIfNull(redirectUrl);

        var uriBuilder = new UriBuilder(redirectUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query[nameof(loginToken)] = loginToken;
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    public Task<string?> GetDeviceIdAsync(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        string? result = null;
        foreach (var (deviceId, token) in accessTokens)
        {
            if (token == accessToken)
            {
                result = deviceId;
                break;
            }
        }
        return Task.FromResult(result);
    }

    public Task<string[]> GetDeviceIdsAsync() => Task.FromResult(accessTokens.Keys.ToArray());

    public async Task<(string userId, string accessToken)?> LogInAsync(string deviceId, string token)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(token);

        (string, string)? result = null;
        if (token == loginToken)
        {
            bool identityCreated = false;
            if (!localIdentityService.HasSelfIdentity)
            {
                (identityCreated, var key) = await CreateOrGetPrivateKeyAsync();
                using var _ = key;
                localIdentityService.UpdateSelfIdentity(identity => identity ?? CreateIdentity(key));
            }
            var identity = localIdentityService.GetSelfIdentity();
            var userId = UserIdentifier.FromId(identity.Id);
            if (identityCreated)
            {
                await userProfile.SetDisplayNameAsync(userId.ToString(), userId.UserName);
            }

            string accessToken = jwtAuthenticator.GenerateToken(userId.ToString());
            accessTokens[deviceId] = accessToken;
            lock (networkProvider)
            {
                networkProvider.Initialize(serverKeys =>
                {
                    if (localIdentityService.Verify(serverKeys) is not null)
                    {
                        return LocalIdentity.Create(serverKeys);
                    }
                    return null;
                });
            }
            result = (userId.ToString(), accessToken);
        }
        return result;
    }

    public async Task<string?> AuthenticateAsync(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        string? userId = await jwtAuthenticator.ValidateToken(accessToken);
        if (userId is not null)
        {
            if (!accessTokens.Values.Contains(accessToken)) // logged out.
            {
                userId = null;
            }
        }
        return userId;
    }

    public Task<int> LogOutAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        accessTokens.TryRemove(deviceId, out string? _);
        return Task.FromResult(accessTokens.Count);
    }

    public Task LogOutAllAsync()
    {
        accessTokens.Clear();
        return Task.CompletedTask;
    }
}
