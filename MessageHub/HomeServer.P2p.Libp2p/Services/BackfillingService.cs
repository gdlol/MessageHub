using MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class BackfillingService : IP2pService
{
    private readonly BackfillingServiceContext context;

    public BackfillingService(BackfillingServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return BackgroundService.Aggregate(
            new ScheduledEventPushingService(context),
            new TopicMemberUpdateEventPullingService(context, p2pNode),
            new TopicMemberUpdateEventPushingService(context, p2pNode),
            new UnresolvedEventPullingService(context, p2pNode));
    }
}
