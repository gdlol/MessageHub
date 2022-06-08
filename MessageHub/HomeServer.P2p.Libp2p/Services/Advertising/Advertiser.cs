namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class Advertiser
{
    private ILogger logger;
    private readonly AdvertisingServiceContext context;
    private readonly Discovery discovery;

    public Advertiser(ILogger logger, AdvertisingServiceContext context, Discovery discovery)
    {
        this.logger = logger;
        this.context = context;
        this.discovery = discovery;
    }

    private async Task Advertise(string topic, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                logger.LogDebug("Advertising topic: {}", topic);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                discovery.Advertise(topic, cts.Token);
                break;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation("Error advertising topic {}: {}", topic, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    public async Task AdvertiseAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Advertising discovery points...");
        try
        {
            var identity = context.IdentityService.GetSelfIdentity();
            stoppingToken.ThrowIfCancellationRequested();
            await Advertise($"p2p:{identity.Id}", stoppingToken);
            var userId = UserIdentifier.FromId(identity.Id);
            string displayName = await context.UserProfile.GetDisplayNameAsync(userId.ToString())
                ?? userId.UserName;
            for (int i = 7; i < 11; i++)
            {
                string peerIdSuffix = identity.Id[^i..];
                string rendezvousPoint = $"{displayName}:{peerIdSuffix}";
                await Advertise(rendezvousPoint, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error advertising discovery points.");
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
