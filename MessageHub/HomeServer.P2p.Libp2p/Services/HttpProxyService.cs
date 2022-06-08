using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class HttpProxyService : IP2pService
{
    public class Context
    {
        private readonly IServer server;

        public ILogger Logger { get; }
        public Uri SelfUri => new(server.Features.Get<IServerAddressesFeature>()!.Addresses.First());

        public Context(ILogger<HttpProxyService> logger, IServer server)
        {
            Logger = logger;
            this.server = server;
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
            context.Logger.LogError(error, "Error running HTTP proxy service.");
        }

        protected override async Task Start(CancellationToken stoppingToken)
        {
            context.Logger.LogInformation("Starting HTTP proxy service...");
            context.Logger.LogInformation("Self URI: {}", context.SelfUri);

            if (!IPAddress.TryParse(context.SelfUri.Host, out var selfAddress)
                || selfAddress.Equals(IPAddress.Any)
                || selfAddress.Equals(IPAddress.IPv6Any))
            {
                selfAddress = IPAddress.Loopback;
            }
            var tcs = new TaskCompletionSource();
            using var _ = stoppingToken.Register(tcs.SetResult);
            using var proxy = p2pNode.Host.StartProxyRequests($"{selfAddress}:{context.SelfUri.Port}");
            await tcs.Task;

            context.Logger.LogInformation("Stopping HTTP proxy service.");
            proxy.Stop();
        }
    }

    private readonly Context context;

    public HttpProxyService(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.context = context;
    }

    BackgroundService IP2pService.Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
