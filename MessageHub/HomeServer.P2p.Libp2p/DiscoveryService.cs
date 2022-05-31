namespace MessageHub.HomeServer.P2p.Libp2p;

internal class DiscoveryService
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly IUserProfile userProfile;
    private CancellationTokenSource? cts;

    public DiscoveryService(
        ILogger<DiscoveryService> logger,
        IIdentityService identityService,
        IUserProfile userProfile)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);

        this.logger = logger;
        this.identityService = identityService;
        this.userProfile = userProfile;
    }

    public void Start(Host host, Discovery discovery)
    {
        if (cts is not null)
        {
            throw new InvalidOperationException();
        }
        logger.LogDebug("Starting discovery service.");
        cts = new CancellationTokenSource();
        var token = cts.Token;
        var identity = identityService.GetSelfIdentity();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    logger.LogDebug("Advertising ID: {}", identity.Id);
                    discovery.Advertise(identity.Id, token);
                    string hostId = host.Id;
                    string userId = UserIdentifier.FromId(identity.Id).ToString();
                    string displayName = await userProfile.GetDisplayNameAsync(userId) ?? identity.Id;
                    for (int i = 7; i < hostId.Length; i++)
                    {
                        string rendezvousPoint = $"/{displayName}/{hostId[..i]}";
                        if (i == 7)
                        {
                            logger.LogDebug("Advertising rendezvousPoints: {}...", rendezvousPoint);
                        }
                        discovery.Advertise(rendezvousPoint, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        logger.LogDebug("Exiting discovery service.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error advertising discovery points.");
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        });
    }

    public void Stop()
    {
        logger.LogDebug("Stopping discovery service.");
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
