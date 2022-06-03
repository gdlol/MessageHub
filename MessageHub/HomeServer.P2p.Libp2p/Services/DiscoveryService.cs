using MessageHub.HomeServer.Services;
using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal class DiscoveryService : IP2pService
{
    public class Context
    {
        public ILogger Logger { get; }
        public IIdentityService IdentityService { get; }
        public IUserProfile UserProfile { get; }

        public Context(ILogger<DiscoveryService> logger, IIdentityService identityService, IUserProfile userProfile)
        {
            Logger = logger;
            IdentityService = identityService;
            UserProfile = userProfile;
        }
    }

    private class Service : ScheduledService
    {
        private readonly Context context;
        private readonly P2pNode p2pNode;

        public Service(Context context, P2pNode p2pNode)
            : base(initialDelay: TimeSpan.FromSeconds(3), interval: TimeSpan.FromMinutes(1))
        {
            this.context = context;
            this.p2pNode = p2pNode;
        }

        protected override void OnError(Exception error)
        {
            context.Logger.LogError(error, "Error running backfilling service.");
        }

        protected override async Task RunAsync(CancellationToken stoppingToken)
        {
            context.Logger.LogInformation("Advertising discovery points...");
            try
            {
                var identity = context.IdentityService.GetSelfIdentity();
                stoppingToken.ThrowIfCancellationRequested();
                context.Logger.LogDebug("Advertising ID: {}", identity.Id);
                p2pNode.Discovery.Advertise(identity.Id, stoppingToken);
                string hostId = p2pNode.Host.Id;
                string userId = UserIdentifier.FromId(identity.Id).ToString();
                string displayName = await context.UserProfile.GetDisplayNameAsync(userId) ?? identity.Id;
                for (int i = 7; i < hostId.Length; i++)
                {
                    string peerIdSuffix = hostId[^i..];
                    string rendezvousPoint = $"/{displayName}/{peerIdSuffix}";
                    if (i == 7)
                    {
                        context.Logger.LogDebug("Advertising rendezvousPoints: {}...", rendezvousPoint);
                    }
                    p2pNode.Discovery.Advertise(rendezvousPoint, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                context.Logger.LogInformation("Error advertising discovery points.", ex.Message);
            }
        }
    }

    private readonly Context context;

    public DiscoveryService(Context context)
    {
        this.context = context;
    }

    public BackgroundService Create(P2pNode p2pNode)
    {
        return new Service(context, p2pNode);
    }
}
