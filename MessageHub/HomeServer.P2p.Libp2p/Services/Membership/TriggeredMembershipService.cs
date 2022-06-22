using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Membership;

internal class TriggeredMembershipService : TriggeredService<MembershipUpdate>
{
    private readonly MembershipServiceContext context;
    private readonly ILogger logger;
    private readonly P2pNode p2pNode;

    public TriggeredMembershipService(MembershipServiceContext context, P2pNode p2pNode)
        : base(context.MembershipUpdateNotifier)
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
            var identity = context.IdentityService.GetSelfIdentity();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 8
            };

            // Verify existing peers.
            IReadOnlySet<string> existingPeers = p2pNode.MemberStore.GetMembers(topic).ToHashSet();
            var verifyExistingPeers = Parallel.ForEachAsync(existingPeers, parallelOptions, async (peerId, token) =>
            {
                logger.LogDebug("Verifying membership of node {} for topic {}...", peerId, topic);
                bool isVerified = false;
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(20));
                    var remoteIdentity = await p2pNode.GetServerIdentityAsync(peerId, linkedCts.Token);
                    if (memberIds.Contains(remoteIdentity?.Id))
                    {
                        logger.LogDebug("Verified membership of node {} for topic {}", peerId, topic);
                        isVerified = true;
                    }
                    else
                    {
                        logger.LogDebug("Node {} (id: {}) is not a member of {}", peerId, remoteIdentity?.Id, topic);
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("Canceled verifying member for topic {}: {}", topic, peerId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Error verifying member {} for topic {}: {}", peerId, topic, ex.Message);
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested && !isVerified)
                    {
                        logger.LogDebug("Removing member for topic {}: {}", topic, peerId);
                        p2pNode.MemberStore.RemoveMember(topic, peerId);
                    }
                }
            });

            // Find new peers.
            var findNewPeers = Parallel.ForEachAsync(memberIds, parallelOptions, async (id, token) =>
            {
                if (id == context.IdentityService.GetSelfIdentity().Id)
                {
                    return;
                }

                logger.LogDebug("Finding nodes for id {} for topic {}...", id, topic);
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(20));
                    var (addressInfo, _) = await p2pNode.Resolver.ResolveAddressInfoAsync(
                        id,
                        cancellationToken: linkedCts.Token);
                    var peerId = Host.GetIdFromAddressInfo(addressInfo);
                    if (existingPeers.Contains(peerId))
                    {
                        logger.LogDebug("Node for id {} for topic {} is in existing peers: {}", id, topic, peerId);
                        return;
                    }
                    logger.LogDebug("Found new peer for topic {}: {}", topic, peerId);
                    p2pNode.MemberStore.AddMember(topic, peerId);
                    context.TopicMemberUpdateNotifier.Notify(new(topic, id));
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.LogDebug("Error getting peer ID for {}: {}", id, ex.Message);
                }
            });

            await Task.WhenAll(verifyExistingPeers, findNewPeers);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error updating members nodes for {}.", topic);
        }
    }
}
