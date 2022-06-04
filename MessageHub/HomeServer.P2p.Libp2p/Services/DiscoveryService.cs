using MessageHub.HomeServer.Notifiers;
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
        public UserProfileUpdateNotifier Notifier { get; }

        public Context(
            ILogger<DiscoveryService> logger,
            IIdentityService identityService,
            IUserProfile userProfile,
            UserProfileUpdateNotifier notifier)
        {
            Logger = logger;
            IdentityService = identityService;
            UserProfile = userProfile;
            Notifier = notifier;
        }
    }

    private class Runner
    {
        private readonly Context context;
        private readonly P2pNode p2pNode;

        public Runner(Context context, P2pNode p2pNode)
        {
            this.context = context;
            this.p2pNode = p2pNode;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            context.Logger.LogInformation("Advertising discovery points...");
            try
            {
                var identity = context.IdentityService.GetSelfIdentity();
                stoppingToken.ThrowIfCancellationRequested();
                context.Logger.LogDebug("Advertising ID: {}", identity.Id);
                p2pNode.Discovery.Advertise(identity.Id, stoppingToken);
                var userId = UserIdentifier.FromId(identity.Id);
                string displayName = await context.UserProfile.GetDisplayNameAsync(userId.ToString())
                    ?? userId.UserName;
                for (int i = 7; i < identity.Id.Length; i++)
                {
                    string peerIdSuffix = identity.Id[^i..];
                    string rendezvousPoint = $"{displayName}:{peerIdSuffix}";
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

    private class ScheduledDiscoveryService : ScheduledService
    {
        private readonly ILogger logger;
        private readonly Runner runner;

        public ScheduledDiscoveryService(ILogger logger, Runner runner)
            : base(initialDelay: TimeSpan.FromSeconds(3), interval: TimeSpan.FromMinutes(1))
        {
            this.logger = logger;
            this.runner = runner;
        }

        protected override void OnError(Exception error)
        {
            logger.LogError(error, "Error running discovery service.");
        }

        protected override Task RunAsync(CancellationToken stoppingToken)
        {
            return runner.RunAsync(stoppingToken);
        }
    }

    private class TriggerDiscoveryService : TriggeredService<UserProfileUpdate>
    {
        private readonly ILogger logger;
        private readonly Runner runner;

        public TriggerDiscoveryService(ILogger logger, Runner runner, UserProfileUpdateNotifier notifier)
            : base(notifier)
        {
            this.logger = logger;
            this.runner = runner;
        }

        protected override void OnError(Exception error)
        {
            logger.LogError(error, "Error running discovery service.");
        }

        protected override Task RunAsync(UserProfileUpdate value, CancellationToken stoppingToken)
        {
            if (value.UpdateType == ProfileUpdateType.DisplayName)
            {
                return runner.RunAsync(stoppingToken);
            }
            else
            {
                return Task.CompletedTask;
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
        var runner = new Runner(context, p2pNode);
        return BackgroundService.Aggregate(
            new ScheduledDiscoveryService(context.Logger, runner),
            new TriggerDiscoveryService(context.Logger, runner, context.Notifier));
    }
}
