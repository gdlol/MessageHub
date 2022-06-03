using MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class PubSubService : IP2pService
{
    private readonly PubSubServiceContext context;

    public PubSubService(PubSubServiceContext context)
    {
        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return BackgroundService.Aggregate(
            new EventPublishService(context),
            new EventSubscriptionService(context, p2pNode));
    }
}
