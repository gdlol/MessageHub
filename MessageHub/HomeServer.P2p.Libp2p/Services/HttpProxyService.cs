using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class HttpProxyService : IP2pService
{
    public class Context
    {
        public ILogger Logger { get; }
        public Uri SelfUri { get; }

        public Context(ILogger<HttpProxyService> logger, IServer server)
        {
            Logger = logger;
            string selfUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
            SelfUri = new Uri(selfUrl);
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

            var addresses = await Dns.GetHostAddressesAsync(context.SelfUri.Host, stoppingToken);
            if (addresses.Length == 0)
            {
                context.Logger.LogError("Cannot resolve self address.");
                return;
            }
            string host = addresses[0].ToString();
            context.Logger.LogInformation("Host: {}", host);

            var tcs = new TaskCompletionSource();
            using var _ = stoppingToken.Register(tcs.SetResult);
            using var proxy = p2pNode.Host.StartProxyRequests($"{host}:{context.SelfUri.Port}");
            await tcs.Task;

            context.Logger.LogInformation("Stopping HTTP proxy service.");
            proxy.Stop();
        }
    }

    private readonly Context context;

    public HttpProxyService(Context context)
    {
        this.context = context;
    }

    BackgroundService IP2pService.Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
