using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class BackfillingServiceContext
{
    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public IRooms Rooms { get; }
    public ITimelineLoader TimelineLoader { get; }
    public IEventSaver EventSaver { get; }
    public UnresolvedEventNotifier UnresolvedEventNotifier { get; }
    public PublishEventNotifier PublishEventNotifier { get; }
    public TopicMemberUpdateNotifier TopicMemberUpdateNotifier { get; }

    public BackfillingServiceContext(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IEventSaver eventSaver,
        UnresolvedEventNotifier unresolvedEventNotifier,
        PublishEventNotifier publishEventNotifier,
        TopicMemberUpdateNotifier topicMemberUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(eventSaver);
        ArgumentNullException.ThrowIfNull(unresolvedEventNotifier);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(topicMemberUpdateNotifier);

        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        Rooms = rooms;
        TimelineLoader = timelineLoader;
        EventSaver = eventSaver;
        UnresolvedEventNotifier = unresolvedEventNotifier;
        PublishEventNotifier = publishEventNotifier;
        TopicMemberUpdateNotifier = topicMemberUpdateNotifier;
    }
}
