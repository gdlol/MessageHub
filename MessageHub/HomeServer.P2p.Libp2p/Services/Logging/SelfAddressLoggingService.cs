using MessageHub.HomeServer.Services;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Logging;

internal class SelfAddressLoggingService : ScheduledService
{
    private readonly ILogger logger;
    private readonly Host host;

    public SelfAddressLoggingService(LoggingServiceContext context, P2pNode p2pNode)
        : base(initialDelay: TimeSpan.FromSeconds(10), interval: TimeSpan.FromMinutes(1))
    {
        logger = context.LoggerFactory.CreateLogger<SelfAddressLoggingService>();
        host = p2pNode.Host;
    }

    protected override void OnError(Exception error)
    {
        logger.LogError(error, "Error running self address logging service.");
    }

    protected override Task RunAsync(CancellationToken stoppingToken)
    {
        string selfAddressInfo = host.GetSelfAddressInfo();
        logger.LogInformation("Self address info: {}", selfAddressInfo);
        return Task.CompletedTask;
    }
}
