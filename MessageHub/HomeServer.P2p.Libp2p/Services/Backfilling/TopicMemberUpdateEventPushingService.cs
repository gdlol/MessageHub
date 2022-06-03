using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class TopicMemberUpdateEventPushingService : QueuedService<TopicMemberUpdate>
{
    private readonly BackfillingServiceContext context;
    private readonly P2pNode p2pNode;
    private readonly ILogger logger;

    public TopicMemberUpdateEventPushingService(BackfillingServiceContext context, P2pNode p2pNode)
        : base(context.TopicMemberUpdateNotifier, boundedCapacity: 16, maxDegreeOfParallelism: 3)
    {
        this.context = context;
        this.p2pNode = p2pNode;
        logger = context.LoggerFactory.CreateLogger<TopicMemberUpdateEventPushingService>();
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running topic member update event pushing service.");
    }

    protected override async Task RunAsync(TopicMemberUpdate value, CancellationToken stoppingToken)
    {
        var (topic, id) = value;
        logger.LogDebug("Pushing latest events of {} to {}...", topic, id);
        try
        {
            string roomId = topic;
            var identity = context.IdentityService.GetSelfIdentity();
            var batchStates = await context.TimelineLoader.LoadBatchStatesAsync(
                x => x == roomId,
                includeLeave: false);
            if (batchStates.RoomEventIds.TryGetValue(roomId, out string? latestEventId))
            {
                var roomEventStore = await context.Rooms.GetRoomEventStoreAsync(roomId);
                var pdu = await roomEventStore.LoadEventAsync(latestEventId);
                string txnId = Guid.NewGuid().ToString();
                var parameters = new PushMessagesRequest
                {
                    Origin = identity.Id,
                    OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Pdus = new[] { pdu }
                };
                var request = identity.SignRequest(
                    destination: id,
                    requestMethod: HttpMethods.Put,
                    requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                    content: parameters);
                await p2pNode.SendAsync(request, stoppingToken);
            }
            else
            {
                logger.LogWarning("Latest event id not found for room {}.", roomId);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Error pushing latest events of {} to {}: {}", topic, id, ex.Message);
        }
    }
}
