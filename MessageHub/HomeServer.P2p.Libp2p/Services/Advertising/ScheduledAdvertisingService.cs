using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class ScheduledAdvertisingService : ScheduledService
{
    private readonly ILogger logger;
    private readonly Advertiser advertiser;

    public ScheduledAdvertisingService(ILogger logger, Advertiser advertiser, TimeSpan interval)
        : base(initialDelay: TimeSpan.FromSeconds(3), interval: interval)
    {
        this.logger = logger;
        this.advertiser = advertiser;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running discovery service.");
    }

    protected override Task RunAsync(CancellationToken stoppingToken)
    {
        return advertiser.AdvertiseAsync(stoppingToken);
    }
}
