using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Web;
using MessageHub.HomeServer.P2p.Providers;
using NSec.Cryptography;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

internal class LocalAuthenticator : IAuthenticator
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
    private readonly string accessTokenPrefix = Guid.NewGuid().ToString();
    private readonly ConcurrentDictionary<string, object?> accessTokens = new();

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

    private async Task<(bool created, Key key)> CreateOrGetPrivateKeyAsync()
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

        string? deviceId = null;
        if (accessTokens.ContainsKey(accessToken))
        {
            deviceId = accessToken[accessTokenPrefix.Length..];
        }
        return Task.FromResult(deviceId);
    }

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
                var (keyId, networkKey) = networkProvider.GetVerifyKey();
                var localIdentity = LocalIdentity.Create(
                    key,
                    new VerifyKeys(new Dictionary<KeyIdentifier, string>
                    {
                        [keyId] = networkKey,
                    }.ToImmutableDictionary(), DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()));
                lock (localIdentityService)
                {
                    if (localIdentityService.HasSelfIdentity)
                    {
                        localIdentity.Dispose();
                        localIdentity = null;
                    }
                    else
                    {
                        localIdentityService.SetSelfIdentity(localIdentity);
                    }
                }
            }
            var identity = localIdentityService.GetSelfIdentity();
            var userId = UserIdentifier.FromId(identity.Id);
            if (identityCreated)
            {
                await userProfile.SetDisplayNameAsync(userId.ToString(), userId.UserName);
            }

            string accessToken = accessTokenPrefix + deviceId;
            if (accessTokens.TryAdd(accessToken, null))
            {
                lock (networkProvider)
                {
                    networkProvider.Initialize(serverKeys =>
                    {
                        if (localIdentityService.Verify(serverKeys))
                        {
                            return LocalIdentity.Create(serverKeys);
                        }
                        return null;
                    });
                }
            }
            result = (userId.ToString(), accessToken);
        }
        return result;
    }

    public Task<string?> AuthenticateAsync(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        if (accessTokens.ContainsKey(accessToken))
        {
            var identity = localIdentityService.GetSelfIdentity();
            return Task.FromResult<string?>(UserIdentifier.FromId(identity.Id).ToString());
        }
        else
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task LogOutAsync(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        string accessToken = accessTokenPrefix + deviceId;
        accessTokens.TryRemove(accessToken, out object? _);
        return Task.CompletedTask;
    }

    public Task LogOutAllAsync()
    {
        accessTokens.Clear();
        return Task.CompletedTask;
    }
}
