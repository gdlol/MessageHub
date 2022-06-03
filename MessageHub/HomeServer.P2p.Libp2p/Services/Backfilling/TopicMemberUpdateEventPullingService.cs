using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class TopicMemberUpdateEventPullingService : QueuedService<TopicMemberUpdate>
{
    private readonly ILogger logger;
    private readonly Backfiller backfiller;

    public TopicMemberUpdateEventPullingService(BackfillingServiceContext context, P2pNode p2pNode)
        : base(context.TopicMemberUpdateNotifier, boundedCapacity: 16, maxDegreeOfParallelism: 3)
    {
        logger = context.LoggerFactory.CreateLogger<TopicMemberUpdateEventPushingService>();
        backfiller = new Backfiller(logger, context, p2pNode);
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running topic member update event pulling service.");
    }

    protected override async Task RunAsync(TopicMemberUpdate value, CancellationToken stoppingToken)
    {
        var (topic, id) = value;
        logger.LogDebug("Pulling latest events of {} from {}...", topic, id);
        try
        {
            await backfiller.PullLatestEventsAsync(topic, id, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Error pulling latest events of {} from {}: {}", topic, id, ex.Message);
        }
    }
}
