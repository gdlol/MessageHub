using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;

internal class EventPublishService : QueuedService<PublishEvent>
{
    private readonly PubSubServiceContext context;
    private readonly ILogger logger;

    public EventPublishService(PubSubServiceContext context)
        : base(context.PublishEventNotifier, boundedCapacity: 1024)
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<PubSubServiceContext>();
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running event publish service.");
    }

    protected override Task RunAsync(PublishEvent value, CancellationToken cancellationToken)
    {
        var (topic, message) = value;
        logger.LogDebug("Publishing message to topic {}...", topic);
        if (!context.JoinedTopics.TryGetValue(topic, out var topicValue))
        {
            logger.LogDebug("Topic not found: {}", topic);
            return Task.CompletedTask;
        }
        var (joinedTopic, _) = topicValue;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            joinedTopic.Publish(message, timeout.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error publishing message to topic {}.", topic);
        }
        return Task.CompletedTask;
    }
}
