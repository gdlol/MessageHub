namespace MessageHub.HomeServer.P2p.Libp2p.Services.Logging;

internal class LoggingServiceContext
{
    public ILogger Logger { get; }

    public LoggingServiceContext(ILogger<LoggingServiceContext> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        Logger = logger;
    }
}
