using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.LocalIdentity;

internal class KeyRotationService : ScheduledService
{
    private readonly ILogger logger;
    private readonly LocalIdentityService identityService;
    private readonly LocalAuthenticator authenticator;

    public KeyRotationService(
        ILogger<KeyRotationService> logger,
        LocalIdentityService identityService,
        LocalAuthenticator authenticator)
        : base(initialDelay: TimeSpan.FromDays(1), interval: TimeSpan.FromDays(1))
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(authenticator);

        this.logger = logger;
        this.identityService = identityService;
        this.authenticator = authenticator;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running key rotation service.");
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        if (!identityService.HasSelfIdentity)
        {
            return;
        }
        var identity = identityService.GetSelfIdentity();
        if (identity.GetServerKeys().ValidUntilTimestamp > DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeMilliseconds())
        {
            return;
        }
        logger.LogInformation("Rotating signing keys...");
        var (_, key) = await authenticator.CreateOrGetPrivateKeyAsync();
        using var _ = key;
        lock (authenticator.CreateIdentity(key))
        {
            var newIdentity = authenticator.CreateIdentity(key);
            identityService.SetSelfIdentity(newIdentity);
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
