using MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class AdvertisingService : IP2pService
{
    private readonly AdvertisingServiceContext context;

    public AdvertisingService(AdvertisingServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return BackgroundService.Aggregate(
            new ScheduledAdvertisingService(context, p2pNode, TimeSpan.FromMinutes(10)),
            new TriggeredAdvertisingService(context, p2pNode));
    }
}
