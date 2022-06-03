using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class MdnsBackgroundService : IP2pService
{
    public class Context
    {
        public ILogger Logger { get; }

        public Context(ILogger<MdnsBackgroundService> logger)
        {
            Logger = logger;
        }
    }

    private class Service : BackgroundService
    {
        private readonly Context context;
        private readonly P2pNode p2pNode;

        public Service(Context context, P2pNode p2pNode)
        {
            this.context = context;
            this.p2pNode = p2pNode;
        }

        protected override void OnError(Exception error)
        {
            context.Logger.LogError(error, "Error running MDNS service.");
        }

        protected override async Task Start(CancellationToken stoppingToken)
        {
            context.Logger.LogInformation("Starting MDNS service...");

            var tcs = new TaskCompletionSource();
            using var _ = stoppingToken.Register(tcs.SetResult);
            using var service = MdnsService.Create(p2pNode.Host, nameof(MessageHub));
            service.Start();
            await tcs.Task;
            
            context.Logger.LogInformation("Stopping MDNS service.");
            service.Stop();
        }
    }

    private readonly Context context;

    public MdnsBackgroundService(Context context)
    {
        this.context = context;
    }

    BackgroundService IP2pService.Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
