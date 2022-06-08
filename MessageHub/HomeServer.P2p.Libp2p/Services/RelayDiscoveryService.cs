using MessageHub.HomeServer.Services;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class RelayDiscoveryService : IP2pService
{
    public class Context
    {
        public ILogger Logger { get; }

        public Context(ILogger<AddressCachingService> logger)
        {
            Logger = logger;
        }
    }

    private class Service : ScheduledService
    {
        private readonly ILogger logger;
        private readonly DHT dht;

        public Service(Context context, P2pNode p2pNode)
            : base(initialDelay: TimeSpan.FromSeconds(5), interval: TimeSpan.FromSeconds(30))
        {
            logger = context.Logger;
            dht = p2pNode.DHT;
        }

        protected override void OnError(Exception error)
        {
            logger.LogError(error, "Error running HTTP proxy service.");
        }

        protected override Task RunAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    dht.FeedClosestPeersToAutoRelay(cts.Token);
                    break;
                }
                catch (OperationCanceledException ex)
                {
                    logger.LogInformation("Error finding auto relay peers: {}", ex.Message);
                }
            }
            return Task.CompletedTask;
        }
    }

    private readonly Context context;

    public RelayDiscoveryService(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    BackgroundService IP2pService.Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
