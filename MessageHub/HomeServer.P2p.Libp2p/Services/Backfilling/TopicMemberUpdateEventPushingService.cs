using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
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
            if (!context.Rooms.HasRoom(roomId))
            {
                logger.LogWarning("Room not found :{}", roomId);
                return;
            }
            var snapshot = await context.Rooms.GetRoomSnapshotAsync(roomId);
            if (snapshot.LatestEventIds.Count >= 20)
            {
                logger.LogWarning("More than 20 ({}) branches in room {}.", snapshot.LatestEventIds.Count, roomId);
            }
            var roomEventStore = await context.Rooms.GetRoomEventStoreAsync(roomId);
            foreach (string[] chunk in snapshot.LatestEventIds.Chunk(100))
            {
                var pdus = new List<PersistentDataUnit>();
                foreach (string eventId in chunk)
                {
                    var pdu = await roomEventStore.LoadEventAsync(eventId);
                    pdus.Add(pdu);
                }
                string txnId = Guid.NewGuid().ToString();
                var parameters = new PushMessagesRequest
                {
                    Origin = identity.Id,
                    OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Pdus = pdus.ToArray()
                };
                var request = identity.SignRequest(
                    destination: id,
                    requestMethod: HttpMethods.Put,
                    requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                    content: parameters);
                await p2pNode.SendAsync(request, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Error pushing latest events of {} to {}: {}", topic, id, ex.Message);
        }
    }
}
