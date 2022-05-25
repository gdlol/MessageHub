using System.Collections.Concurrent;
using System.Text.Json;

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

        public static PublishLoop Create(ConcurrentDictionary<string, Topic> joinedTopics, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<PublishLoop>();
            var loop = new PublishLoop();
            Task.Run(() =>
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
            });
            return loop;
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            messageQueue.Dispose();
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
            Action<string, JsonElement> subscriber)
        {
            var logger = loggerFactory.CreateLogger<SubscribeLoop>();
            var loop = new SubscribeLoop(subscription);
            using var _ = loop.Token.Register(subscription.Cancel);
            Task.Run(() =>
            {
                while (true)
                {
                    loop.Token.ThrowIfCancellationRequested();
                    var (topic, message) = subscription.Next(loop.Token);
                    try
                    {
                        subscriber(topic, message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error handling message to topic {}", topic);
                    }
                }
            });
            return loop;
        }
    }

    private readonly PubSub pubsub;
    private readonly Action<string, JsonElement> subscriber;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, Topic> joinedTopics;
    private PublishLoop? publishLoop;
    private readonly ConcurrentDictionary<string, SubscribeLoop> subscribeLoops;

    public PubSubService(PubSub pubsub, Action<string, JsonElement> subscriber, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(pubsub);
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.pubsub = pubsub;
        this.subscriber = subscriber;
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<PubSubService>();
        joinedTopics = new ConcurrentDictionary<string, Topic>();
        subscribeLoops = new ConcurrentDictionary<string, SubscribeLoop>();
    }

    public void Start()
    {
        if (publishLoop is not null)
        {
            throw new InvalidOperationException();
        }
        publishLoop = PublishLoop.Create(joinedTopics, loggerFactory);
    }

    public void Stop()
    {
        if (publishLoop is null)
        {
            return;
        }
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
            topic.Close();
            topic.Dispose();
        }
        joinedTopics.Clear();
    }

    public void Publish(string topic, JsonElement message)
    {
        if (publishLoop is null)
        {
            throw new InvalidOperationException();
        }
        publishLoop.Publish(topic, message);
    }

    public void JoinTopic(string topic)
    {
        var joinedTopic = joinedTopics.GetOrAdd(topic, _ => pubsub.JoinTopic(topic));
        subscribeLoops.GetOrAdd(topic, _ =>
        {
            var subscription = joinedTopic.Subscribe();
            return SubscribeLoop.Create(subscription, loggerFactory, subscriber);
        });
    }

    public void LeaveTopic(string topic)
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
            joinedTopic.Close();
            joinedTopic.Dispose();
        }
        else
        {
            logger.LogWarning("Topic not found: {}", topic);
        }
    }
}
