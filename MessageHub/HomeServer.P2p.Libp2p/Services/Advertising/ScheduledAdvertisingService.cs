using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class ScheduledAdvertisingService : ScheduledService
{
    private readonly ILogger logger;
    private readonly Advertiser advertiser;
    private readonly TimeSpan interval;

    public ScheduledAdvertisingService(AdvertisingServiceContext context, P2pNode p2pNode, TimeSpan interval)
        : base(initialDelay: TimeSpan.FromSeconds(3), interval: interval)
    {
        logger = context.LoggerFactory.CreateLogger<ScheduledAdvertisingService>();
        advertiser = new Advertiser(logger, context, p2pNode);
        this.interval = interval;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running discovery service.");
    }

    protected override Task RunAsync(CancellationToken stoppingToken)
    {
        return advertiser.AdvertiseAsync(3 * interval, stoppingToken);
    }
}
