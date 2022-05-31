using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.HomeServer.Events.Room;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.HomeServer.Rooms.Timeline;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class MembershipService
{
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly ITimelineLoader timelineLoader;
    private readonly IRooms rooms;
    private readonly MembershipUpdateNotifier notifier;
    private EventHandler<(string, string[])>? onNotify;

    public MembershipService(
        ILogger<MembershipService> logger,
        IIdentityService identityService,
        ITimelineLoader timelineLoader,
        IRooms rooms,
        MembershipUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(timelineLoader);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(notifier);

        this.logger = logger;
        this.identityService = identityService;
        this.timelineLoader = timelineLoader;
        this.rooms = rooms;
        this.notifier = notifier;
    }

    public void Start(MemberStore memberStore, IPeerResolver peerResolver)
    {
        if (onNotify is not null)
        {
            throw new InvalidOperationException();
        }
        logger.LogDebug("Starting membership service.");
        onNotify = (sender, e) =>
        {
            Task.Run(async () =>
            {
                var (topic, memberIds) = e;
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = 8
                };
                var members = new ConcurrentBag<string>();
                await Parallel.ForEachAsync(memberIds, parallelOptions, async (id, token) =>
                {
                    if (id == identityService.GetSelfIdentity().Id)
                    {
                        return;
                    }
                    try
                    {
                        var addressInfo = await peerResolver.ResolveAddressInfoAsync(id, cancellationToken: token);
                        var peerId = Host.GetIdFromAddressInfo(addressInfo);
                        members.Add(peerId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error getting peer ID for {}", id);
                    }
                });
                var existingMembers = memberStore.GetMembers(topic);
                var newMembers = members.Except(existingMembers);
                var oldMembers = existingMembers.Except(members);
                foreach (string member in newMembers)
                {
                    memberStore.AddMember(topic, member);
                }
                foreach (string member in oldMembers)
                {
                    memberStore.RemoveMember(topic, member);
                }
            });
        };
        notifier.OnNotify += onNotify;
        _ = Task.Run(async () =>
        {
            // Update room memberships.
            var batchStates = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
            foreach (var roomId in batchStates.JoinedRoomIds)
            {
                var snapshot = await rooms.GetRoomSnapshotAsync(roomId);
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
                        members.Add(userIdentifier.PeerId);
                    }
                }
                notifier.Notify((roomId, members.ToArray()));
            }
        });
    }

    public void Stop()
    {
        if (onNotify is not null)
        {
            logger.LogDebug("Stopping membership service.");
            notifier.OnNotify -= onNotify;
        }
    }
}
