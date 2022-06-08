using MessageHub.HomeServer.P2p.Libp2p.Services.Membership;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class MembershipService : IP2pService
{
    private readonly MembershipServiceContext context;

    public MembershipService(MembershipServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return BackgroundService.Aggregate(
            new MembershipUpdateTriggerService(context),
            new TriggeredMembershipService(context, p2pNode));
    }
}
