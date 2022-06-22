using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class TriggeredAdvertisingService : TriggeredService<UserProfileUpdate>
{
    private readonly ILogger logger;
    private readonly Advertiser advertiser;
    private readonly TimeSpan ttl;

    public TriggeredAdvertisingService(AdvertisingServiceContext context, P2pNode p2pNode, TimeSpan ttl)
        : base(context.Notifier)
    {
        logger = context.LoggerFactory.CreateLogger<TriggeredAdvertisingService>();
        advertiser = new Advertiser(logger, context, p2pNode);
        this.ttl = ttl;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running discovery service.");
    }

    protected override Task RunAsync(UserProfileUpdate value, CancellationToken stoppingToken)
    {
        if (value.UpdateType == ProfileUpdateType.DisplayName)
        {
            return advertiser.AdvertiseAsync(ttl, stoppingToken);
        }
        else
        {
            return Task.CompletedTask;
        }
    }
}
