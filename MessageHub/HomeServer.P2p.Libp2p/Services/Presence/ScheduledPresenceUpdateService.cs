using MessageHub.HomeServer.Events.General;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Presence;

internal class ScheduledPresenceUpdateService : ScheduledService
{
    private readonly ILogger logger;
    private readonly PresenceServiceContext context;
    private readonly SelfPresencePublisher publisher;

    public ScheduledPresenceUpdateService(PresenceServiceContext context)
        : base(initialDelay: TimeSpan.FromSeconds(10), interval: TimeSpan.FromMinutes(1), jitterRange: 0.1)
    {
        logger = context.LoggerFactory.CreateLogger<TriggeredPresenceUpdateService>();
        this.context = context;
        publisher = new SelfPresencePublisher(context);
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running presence update service.");
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("updating presence status...");
        try
        {
            var identity = context.IdentityService.GetSelfIdentity();
            bool notifyTimelineUpdate = false;
            foreach (var (userId, timestamp) in context.UserPresence.GetLatestUpdateTimestamps())
            {
                var delta = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                if (userId == UserIdentifier.FromId(identity.Id).ToString())
                {
                    if (delta > TimeSpan.FromMinutes(5))
                    {
                        var currentStatus = context.UserPresence.GetPresence(userId);
                        if (currentStatus?.Presence == PresenceValues.Online)
                        {
                            context.UserPresence.SetPresence(userId, PresenceValues.Unavailable, null);
                            notifyTimelineUpdate = true;
                        }
                    }
                }
                else
                {
                    if (delta > TimeSpan.FromMinutes(10))
                    {
                        context.UserPresence.SetPresence(userId, PresenceValues.Offline, null);
                        notifyTimelineUpdate = true;
                    }
                }
            }
            if (notifyTimelineUpdate)
            {
                context.TimelineUpdateNotifier.Notify();
            }
            await publisher.PublishAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error updating presence status.");
        }
    }
}
