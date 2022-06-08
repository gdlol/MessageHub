using MessageHub.HomeServer.P2p.Libp2p.Services.Logging;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class LoggingService : IP2pService
{
    private readonly LoggingServiceContext context;

    public LoggingService(LoggingServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return new SelfAddressLoggingService(context.Logger, p2pNode.Host);
    }
}
