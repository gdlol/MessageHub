using System.Text.Json;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Services;
using Microsoft.Extensions.Caching.Memory;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class AddressCachingService : IP2pService
{
    public class Context
    {
        public ILogger Logger { get; }
        public IMemoryCache AddressCache { get; }
        public IIdentityService IdentityService { get; }
        public AuthenticatedRequestNotifier Notifier { get; }

        public Context(
            ILogger<AddressCachingService> logger,
            IMemoryCache addressCache,
            IIdentityService identityService,
            AuthenticatedRequestNotifier notifier)
        {
            Logger = logger;
            AddressCache = addressCache;
            IdentityService = identityService;
            Notifier = notifier;
        }
    }

    private class Service : TriggeredService<ServerKeys>
    {
        private readonly Context context;
        private readonly P2pNode p2pNode;

        public Service(Context context, P2pNode p2pNode) : base(context.Notifier)
        {
            this.context = context;
            this.p2pNode = p2pNode;
        }

        protected override void OnError(Exception error)
        {
            context.Logger.LogError(error, "Error running HTTP proxy service.");
        }

        protected override Task RunAsync(ServerKeys value, CancellationToken stoppingToken)
        {
            var identity = context.IdentityService.GetSelfIdentity();
            var remoteIdentity = p2pNode.TryGetIdentity(value);
            if (remoteIdentity is null)
            {
                context.Logger.LogWarning("Identity verification failed: {}", JsonSerializer.SerializeToElement(value));
                return Task.CompletedTask;
            }
            if (identity.Id == remoteIdentity.Id)
            {
                return Task.CompletedTask;
            }
            if (context.AddressCache.TryGetValue(remoteIdentity.Id, out var _))
            {
                return Task.CompletedTask;
            }
            if (!value.VerifyKeys.TryGetValue(AuthorizedPeer.KeyIdentifier, out var peerId))
            {
                context.Logger.LogWarning("PeerId not found: {}", JsonSerializer.SerializeToElement(value));
            }
            else
            {
                string? addressInfo = p2pNode.Host.TryGetAddressInfo(peerId);
                if (addressInfo is not null)
                {
                    context.Logger.LogDebug("Setting address of {} to cache: {}", peerId, addressInfo);
                    context.AddressCache.Set(
                        remoteIdentity.Id,
                        addressInfo,
                        DateTimeOffset.FromUnixTimeMilliseconds(value.ValidUntilTimestamp));
                    p2pNode.Host.Protect(Host.GetIdFromAddressInfo(addressInfo), nameof(MessageHub));
                }
            }
            return Task.CompletedTask;
        }
    }

    private readonly Context context;

    public AddressCachingService(Context context)
    {
        this.context = context;
    }

    BackgroundService IP2pService.Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
