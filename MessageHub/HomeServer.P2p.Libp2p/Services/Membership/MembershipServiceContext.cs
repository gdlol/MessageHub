using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Membership;

internal class MembershipServiceContext
{
    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public IRooms Rooms { get; }
    public ITimelineLoader TimelineLoader { get; }
    public MembershipUpdateNotifier MembershipUpdateNotifier { get; }
    public TopicMemberUpdateNotifier TopicMemberUpdateNotifier { get; }

    public MembershipServiceContext(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        MembershipUpdateNotifier membershipUpdateNotifier,
        TopicMemberUpdateNotifier topicMemberUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);
        ArgumentNullException.ThrowIfNull(topicMemberUpdateNotifier);

        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        Rooms = rooms;
        TimelineLoader = timelineLoader;
        MembershipUpdateNotifier = membershipUpdateNotifier;
        TopicMemberUpdateNotifier = topicMemberUpdateNotifier;
    }
}
