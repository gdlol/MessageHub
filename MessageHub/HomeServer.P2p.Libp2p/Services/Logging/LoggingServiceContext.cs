namespace MessageHub.HomeServer.P2p.Libp2p.Services.Logging;

internal class LoggingServiceContext
{
    public ILoggerFactory LoggerFactory { get; }

    public LoggingServiceContext(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        LoggerFactory = loggerFactory;
    }
}
