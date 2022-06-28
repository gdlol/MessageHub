using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

internal class KeyRotationService : ScheduledService
{
    private readonly ILogger logger;
    private readonly LocalIdentityService localIdentityService;
    private readonly LocalAuthenticator localAuthenticator;

    public KeyRotationService(
        ILogger<KeyRotationService> logger,
        LocalIdentityService localIdentityService,
        LocalAuthenticator localAuthenticator)
        : base(initialDelay: TimeSpan.FromDays(1), interval: TimeSpan.FromDays(1))
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(localIdentityService);
        ArgumentNullException.ThrowIfNull(localAuthenticator);

        this.logger = logger;
        this.localIdentityService = localIdentityService;
        this.localAuthenticator = localAuthenticator;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running key rotation service.");
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        if (!localIdentityService.HasSelfIdentity)
        {
            return;
        }
        var identity = localIdentityService.GetSelfIdentity();
        if (identity.GetServerKeys().ValidUntilTimestamp > DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeMilliseconds())
        {
            return;
        }
        logger.LogInformation("Rotating signing keys...");
        var (_, key) = await localAuthenticator.CreateOrGetPrivateKeyAsync();
        using var _ = key;
        lock (localIdentityService)
        {
            var newIdentity = localAuthenticator.CreateIdentity(key);
            localIdentityService.SetSelfIdentity(newIdentity);
            identity.Dispose();
        }
        logger.LogInformation("Updated self identity.");
    }
}

internal class HostedKeyRotationService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly KeyRotationService keyRotationService;

    public HostedKeyRotationService(KeyRotationService keyRotationService)
    {
        ArgumentNullException.ThrowIfNull(keyRotationService);

        this.keyRotationService = keyRotationService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource();
        using var _ = stoppingToken.Register(tcs.SetResult);
        keyRotationService.Start();
        return tcs.Task;
    }
}
