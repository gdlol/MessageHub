using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Presence;

internal class TriggeredPresenceUpdateService : TriggeredService
{
    private readonly ILogger logger;
    private readonly SelfPresencePublisher publisher;

    public TriggeredPresenceUpdateService(PresenceServiceContext context)
        : base(context.PresenceUpdateNotifier)
    {
        logger = context.LoggerFactory.CreateLogger<TriggeredPresenceUpdateService>();
        publisher = new SelfPresencePublisher(context);
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running presence update service.");
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("Publishing presence status update...");
        try
        {
            await publisher.PublishAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error publishing presence status.");
        }
    }
}
