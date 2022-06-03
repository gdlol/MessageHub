using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;

internal class EventSubscriptionService : TriggeredService<MembershipUpdate>
{
    private readonly PubSubServiceContext context;
    private readonly ILogger logger;
    private readonly P2pNode p2pNode;

    public EventSubscriptionService(PubSubServiceContext context, P2pNode p2pNode)
        : base(context.MembershipUpdateNotifier)
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<EventSubscriptionService>();
        this.p2pNode = p2pNode;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running event subscription service.");
    }

    private Task Subscribe(Topic topic, CancellationToken cancellationToken)
    {
        string selfId = p2pNode.Host.Id;
        using var subscription = topic.Subscribe();
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (sender, message) = subscription.Next(cancellationToken);
                if (sender == selfId)
                {
                    continue;
                }
                context.RemoteRequestNotifier.Notify(new(subscription.Topic, message));
            }
        }
        finally
        {
            subscription.Cancel();
        }
    }

    protected override Task RunAsync(MembershipUpdate value, CancellationToken stoppingToken)
    {
        var identity = context.IdentityService.GetSelfIdentity();
        var (topic, ids) = value;
        try
        {
            if (ids.Contains(identity.Id))
            {
                bool newTopic = false;
                var (joinedTopic, cts) = context.JoinedTopics.GetOrAdd(topic, _ =>
                {
                    logger.LogInformation("Joining topic {}...", topic);
                    newTopic = true;
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    return (p2pNode.Pubsub.JoinTopic(topic), cts);
                });
                if (newTopic)
                {
                    Task.Run(async () =>
                    {
                        using var _ = joinedTopic;
                        using var __ = cts;
                        logger.LogDebug("Start subscribing messages for topic {}...", topic);
                        try
                        {
                            await Subscribe(joinedTopic, cts.Token);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error subscribing messages for topic {}.", topic);
                        }
                        try
                        {
                            joinedTopic.Close();
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning("Error closing topic {}: {}", topic, ex.Message);
                        }
                        logger.LogDebug("Stop subscribing messages for topic {}.", topic);
                        context.JoinedTopics.TryRemove(topic, out var _);
                    }, default);
                }
            }
            else
            {
                if (context.JoinedTopics.TryGetValue(topic, out var topicValue))
                {
                    logger.LogInformation("Leaving topic {}...", topic);
                    var (_, cts) = topicValue;
                    cts.Cancel();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error updating topic subscription.");
        }
        return Task.CompletedTask;
    }
}
