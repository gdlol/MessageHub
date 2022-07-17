using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Backfilling;

internal class ScheduledEventPushingService : ScheduledService
{
    private readonly BackfillingServiceContext context;
    private readonly ILogger logger;

    public ScheduledEventPushingService(BackfillingServiceContext context)
        : base(initialDelay: TimeSpan.FromSeconds(3), interval: TimeSpan.FromMinutes(1))
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<ScheduledEventPushingService>();
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running backfilling service.");
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Advertising latest events...");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = context.IdentityService.GetSelfIdentity();
            var batchStates = await context.TimelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
            foreach (string roomId in batchStates.JoinedRoomIds)
            {
                if (!context.Rooms.HasRoom(roomId))
                {
                    logger.LogError("Room not found :{}", roomId);
                    continue;
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
                    var request = new PushMessagesRequest
                    {
                        Origin = identity.Id,
                        OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Pdus = pdus.ToArray()
                    };
                    var signedRequest = identity.SignRequest(
                        destination: roomId,
                        requestMethod: HttpMethods.Put,
                        requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                        content: request);
                    context.PublishEventNotifier.Notify(new(roomId, signedRequest.ToJsonElement()));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error advertising latest events.");
        }
    }
}
