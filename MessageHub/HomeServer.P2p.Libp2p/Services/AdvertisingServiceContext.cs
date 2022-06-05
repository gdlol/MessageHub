using MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class AdvertisingService : IP2pService
{
    private readonly AdvertisingServiceContext context;

    public AdvertisingService(AdvertisingServiceContext context)
    {
        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        var advertiser = new Advertiser(context, p2pNode.Discovery);
        return BackgroundService.Aggregate(
            new ScheduledAdvertisingService(context.Logger, advertiser, TimeSpan.FromMinutes(10)),
            new TriggeredAdvertisingService(context.Logger, advertiser, context.Notifier));
    }
}
