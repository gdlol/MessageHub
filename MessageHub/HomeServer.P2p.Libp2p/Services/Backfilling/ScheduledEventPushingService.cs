using MessageHub.Federation;
using MessageHub.Federation.Protocol;
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
        logger.LogDebug("Advertising latest events...");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = context.IdentityService.GetSelfIdentity();
            var batchStates = await context.TimelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
            foreach (string roomId in batchStates.JoinedRoomIds)
            {
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
                        destination: roomId,
                        requestMethod: HttpMethods.Put,
                        requestTarget: $"/_matrix/federation/v1/send/{txnId}",
                        content: parameters);
                    context.PublishEventNotifier.Notify(new(roomId, request.ToJsonElement()));
                }
                else
                {
                    logger.LogWarning("Latest event id not found for room {}.", roomId);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error advertising latest events.");
        }
    }
}
