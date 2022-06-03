using System.Text.Json;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Membership;

internal class MembershipUpdateTriggerService : ScheduledService
{
    private readonly MembershipServiceContext context;
    private readonly ILogger logger;

    public MembershipUpdateTriggerService(MembershipServiceContext context)
        : base(initialDelay: TimeSpan.FromSeconds(3), interval: TimeSpan.FromMinutes(1))
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<MembershipUpdateTriggerService>();
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error triggering membership service.");
    }

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Triggering membership update...");
        try
        {
            var batchStates = await context.TimelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
            foreach (var roomId in batchStates.JoinedRoomIds)
            {
                var snapshot = await context.Rooms.GetRoomSnapshotAsync(roomId);
                var members = new List<string>();
                foreach (var (roomStateKey, content) in snapshot.StateContents)
                {
                    if (roomStateKey.EventType != EventTypes.Member)
                    {
                        continue;
                    }
                    var memberEvent = JsonSerializer.Deserialize<MemberEvent>(content)!;
                    if (memberEvent.MemberShip == MembershipStates.Join)
                    {
                        var userIdentifier = UserIdentifier.Parse(roomStateKey.StateKey);
                        members.Add(userIdentifier.Id);
                    }
                }
                logger.LogDebug("Found {} members for {}.", members.Count, roomId);
                context.MembershipUpdateNotifier.Notify(new(roomId, members.ToArray()));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error Triggering membership update.");
        }
    }
}
