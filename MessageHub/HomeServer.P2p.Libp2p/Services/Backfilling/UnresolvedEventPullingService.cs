using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class UnresolvedEventPullingService : QueuedService<PersistentDataUnit[]>
{
    private readonly ILogger logger;
    private readonly Backfiller backfiller;

    public UnresolvedEventPullingService(BackfillingServiceContext context, P2pNode p2pNode)
        : base(context.UnresolvedEventNotifier, boundedCapacity: 1024, maxDegreeOfParallelism: 3)
    {
        logger = context.LoggerFactory.CreateLogger<UnresolvedEventPullingService>();
        backfiller = new Backfiller(logger, context, p2pNode);
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running backfilling service.");
    }

    protected override async Task RunAsync(PersistentDataUnit[] value, CancellationToken cancellationToken)
    {
        logger.LogDebug("Backfilling events for {}...", value.FirstOrDefault()?.RoomId);
        try
        {
            await backfiller.BackfillAsync(value, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error backfilling events for {}.", value.FirstOrDefault()?.RoomId);
        }
    }
}
