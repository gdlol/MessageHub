using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Membership;

internal class TriggeredMembershipService : TriggeredService<MembershipUpdate>
{
    private readonly MembershipServiceContext context;
    private readonly ILogger logger;
    private readonly P2pNode p2pNode;

    public TriggeredMembershipService(MembershipServiceContext context, P2pNode p2pNode) : base(context.MembershipUpdateNotifier)
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<TriggeredMembershipService>();
        this.p2pNode = p2pNode;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running membership service.");
    }

    protected override async Task RunAsync(MembershipUpdate value, CancellationToken cancellationToken)
    {
        var (topic, memberIds) = value;
        logger.LogInformation("Updating members nodes for {}...", topic);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 8
            };
            var members = new ConcurrentDictionary<string, string>();
            await Parallel.ForEachAsync(memberIds, parallelOptions, async (id, token) =>
            {
                if (id == context.IdentityService.GetSelfIdentity().Id)
                {
                    return;
                }
                try
                {
                    var (addressInfo, _) = await p2pNode.Resolver.ResolveAddressInfoAsync(id, cancellationToken: token);
                    var peerId = Host.GetIdFromAddressInfo(addressInfo);
                    members.TryAdd(peerId, id);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.LogDebug("Error getting peer ID for {}: {}", id, ex.Message);
                }
            });
            var existingMembers = p2pNode.MemberStore.GetMembers(topic);
            var newMembers = members.Keys.Except(existingMembers);
            var oldMembers = existingMembers.Except(members.Keys);
            var topicMemberUpdates = new HashSet<TopicMemberUpdate>();
            foreach (string peerId in newMembers)
            {
                logger.LogDebug("Found new member for topic {}: {}", peerId, topic);
                p2pNode.MemberStore.AddMember(topic, peerId);
                topicMemberUpdates.Add(new(topic, members[peerId]));
            }
            foreach (string peerId in oldMembers)
            {
                logger.LogDebug("Removing member for topic {}: {}", peerId, topic);
                p2pNode.MemberStore.RemoveMember(topic, peerId);
            }
            foreach (var update in topicMemberUpdates)
            {
                context.TopicMemberUpdateNotifier.Notify(update);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error updating members nodes for {}.", topic);
        }
    }
}
