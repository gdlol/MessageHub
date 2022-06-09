using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class TriggeredAdvertisingService : TriggeredService<UserProfileUpdate>
{
    private readonly ILogger logger;
    private readonly Advertiser advertiser;

    public TriggeredAdvertisingService(AdvertisingServiceContext context, P2pNode p2pNode)
        : base(context.Notifier)
    {
        logger = context.LoggerFactory.CreateLogger<TriggeredAdvertisingService>();
        advertiser = new Advertiser(logger, context, p2pNode);
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running discovery service.");
    }

    protected override Task RunAsync(UserProfileUpdate value, CancellationToken stoppingToken)
    {
        if (value.UpdateType == ProfileUpdateType.DisplayName)
        {
            return advertiser.AdvertiseAsync(stoppingToken);
        }
        else
        {
            return Task.CompletedTask;
        }
    }
}
