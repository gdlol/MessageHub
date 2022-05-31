using System.Collections.Concurrent;
using System.Text.Json;
using MessageHub.HomeServer.P2p.Notifiers;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class PubSubService
{
    private class PublishLoop : IDisposable
    {
        private readonly CancellationTokenSource cts;
        private readonly BlockingCollection<(string, JsonElement)> messageQueue;

        private PublishLoop()
        {
            cts = new CancellationTokenSource();
            messageQueue = new BlockingCollection<(string, JsonElement)>(boundedCapacity: 1024);
        }

        public CancellationToken Token => cts.Token;

        public void Cancel() => cts.Cancel();

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            messageQueue.Dispose();
        }

        public static PublishLoop Create(ConcurrentDictionary<string, Topic> joinedTopics, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<PublishLoop>();
            var loop = new PublishLoop();
            Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        loop.Token.ThrowIfCancellationRequested();
                        var (topic, message) = loop.messageQueue.Take();
                        if (!joinedTopics.TryGetValue(topic, out var joinedTopic))
                        {
                            logger.LogDebug("Topic not found: {}", topic);
                            continue;
                        }
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(loop.Token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(10));
                        try
                        {
                            joinedTopic.Publish(message, timeout.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            loop.Token.ThrowIfCancellationRequested();
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error publishing to topic {}", topic);
                        }
                    }
                }
                finally
                {
                    logger.LogDebug("Exiting publish loop.");
                }
            });
            return loop;
        }

        public void Publish(string topic, JsonElement message)
        {
            messageQueue.TryAdd((topic, message));
        }
    }

    private class SubscribeLoop
    {
        private readonly CancellationTokenSource cts;
        private readonly Subscription subscription;

        private SubscribeLoop(Subscription subscription)
        {
            cts = new CancellationTokenSource();
            this.subscription = subscription;
        }

        public CancellationToken Token => cts.Token;

        public void Cancel() => cts.Cancel();

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            subscription.Dispose();
        }

        public static SubscribeLoop Create(
            Subscription subscription,
            ILoggerFactory loggerFactory,
            RemoteRequestNotifier notifier)
        {
            var logger = loggerFactory.CreateLogger<SubscribeLoop>();
            var loop = new SubscribeLoop(subscription);
            Task.Run(() =>
            {
                using var _ = loop.Token.Register(subscription.Cancel);
                try
                {
                    while (true)
                    {
                        loop.Token.ThrowIfCancellationRequested();
                        var (sender, message) = subscription.Next(loop.Token);
                        try
                        {
                            notifier.Notify((subscription.Topic, message));
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error handling message to topic {}", subscription.Topic);
                        }
                    }
                }
                finally
                {
                    logger.LogDebug("Exiting subscribe loop {}.", subscription.Topic);
                }
            });
            return loop;
        }
    }

    private readonly MembershipUpdateNotifier notifier;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly IIdentityService identityService;
    private readonly RemoteRequestNotifier remoteRequestNotifier;
    private readonly ConcurrentDictionary<string, Topic> joinedTopics;
    private PublishLoop? publishLoop;
    private readonly ConcurrentDictionary<string, SubscribeLoop> subscribeLoops;
    private EventHandler<(string, string[])>? onNotify;

    public PubSubService(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        RemoteRequestNotifier remoteRequestNotifier,
        MembershipUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(remoteRequestNotifier);
        ArgumentNullException.ThrowIfNull(notifier);

        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<PubSubService>();
        this.identityService = identityService;
        this.remoteRequestNotifier = remoteRequestNotifier;
        this.notifier = notifier;
        joinedTopics = new ConcurrentDictionary<string, Topic>();
        subscribeLoops = new ConcurrentDictionary<string, SubscribeLoop>();
    }

    public void Start(PubSub pubsub)
    {
        if (publishLoop is not null)
        {
            throw new InvalidOperationException();
        }
        logger.LogDebug("Starting pubsub service.");
        publishLoop = PublishLoop.Create(joinedTopics, loggerFactory);

        void JoinTopic(string topic)
        {
            var joinedTopic = joinedTopics.GetOrAdd(topic, _ => pubsub.JoinTopic(topic));
            subscribeLoops.GetOrAdd(topic, _ =>
            {
                var subscription = joinedTopic.Subscribe();
                return SubscribeLoop.Create(subscription, loggerFactory, remoteRequestNotifier);
            });
        }

        void LeaveTopic(string topic)
        {
            if (subscribeLoops.TryRemove(topic, out var subscribeLoop))
            {
                subscribeLoop.Cancel();
                subscribeLoop.Dispose();
            }
            else
            {
                logger.LogWarning("Subscription not found for topic {}", topic);
            }
            if (joinedTopics.TryRemove(topic, out var joinedTopic))
            {
                using var _ = joinedTopic;
                joinedTopic.Close();
            }
            else
            {
                logger.LogWarning("Topic not found: {}", topic);
            }
        }

        onNotify = (sender, e) =>
        {
            if (!identityService.HasSelfIdentity)
            {
                return;
            }
            var identity = identityService.GetSelfIdentity();
            var (topic, ids) = e;
            if (ids.Contains(identity.Id))
            {
                logger.LogInformation("Joining topic {}...", topic);
                JoinTopic(topic);
            }
            else
            {
                logger.LogInformation("Leaving topic {}...", topic);
                LeaveTopic(topic);
            }
        };
        notifier.OnNotify += onNotify;
    }

    public void Stop()
    {
        if (publishLoop is null)
        {
            return;
        }
        logger.LogDebug("Stopping pubsub service.");
        publishLoop.Cancel();
        publishLoop.Dispose();
        publishLoop = null;
        foreach (var subscription in subscribeLoops.Values)
        {
            subscription.Cancel();
            subscription.Dispose();
        }
        subscribeLoops.Clear();
        foreach (var topic in joinedTopics.Values)
        {
            topic.Dispose();
        }
        joinedTopics.Clear();
        if (onNotify is not null)
        {
            notifier.OnNotify -= onNotify;
            onNotify = null;
        }
    }

    public void Publish(string topic, JsonElement message)
    {
        if (publishLoop is null)
        {
            throw new InvalidOperationException();
        }
        publishLoop.Publish(topic, message);
    }
}
