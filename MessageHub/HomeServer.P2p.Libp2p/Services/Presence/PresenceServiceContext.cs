using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Presence;

internal class PresenceServiceContext
{
    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public IRooms Rooms { get; }
    public ITimelineLoader TimelineLoader { get; }
    public IUserPresence UserPresence { get; }
    public PresenceUpdateNotifier PresenceUpdateNotifier { get; }
    public PublishEventNotifier PublishEventNotifier { get; }
    public TimelineUpdateNotifier TimelineUpdateNotifier { get; }

    public PresenceServiceContext(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        IRooms rooms,
        ITimelineLoader timelineLoader,
        IUserPresence userPresence,
        PresenceUpdateNotifier presenceUpdateNotifier,
        PublishEventNotifier publishEventNotifier,
        TimelineUpdateNotifier timelineUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(userPresence);
        ArgumentNullException.ThrowIfNull(presenceUpdateNotifier);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(timelineUpdateNotifier);

        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        Rooms = rooms;
        TimelineLoader = timelineLoader;
        UserPresence = userPresence;
        PresenceUpdateNotifier = presenceUpdateNotifier;
        PublishEventNotifier = publishEventNotifier;
        TimelineUpdateNotifier = timelineUpdateNotifier;
    }
}
