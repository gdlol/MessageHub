using MessageHub.HomeServer.P2p.Libp2p.Services.Presence;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class PresenceService : IP2pService
{
    private readonly PresenceServiceContext context;

    public PresenceService(PresenceServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return BackgroundService.Aggregate(
            new TriggeredPresenceUpdateService(context),
            new ScheduledPresenceUpdateService(context));
    }
}
