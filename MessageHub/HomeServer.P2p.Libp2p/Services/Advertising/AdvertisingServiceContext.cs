using MessageHub.HomeServer.Notifiers;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class AdvertisingServiceContext
{
    public ILogger Logger { get; }
    public IIdentityService IdentityService { get; }
    public IUserProfile UserProfile { get; }
    public UserProfileUpdateNotifier Notifier { get; }

    public AdvertisingServiceContext(
        ILogger<AdvertisingServiceContext> logger,
        IIdentityService identityService,
        IUserProfile userProfile,
        UserProfileUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(notifier);

        Logger = logger;
        IdentityService = identityService;
        UserProfile = userProfile;
        Notifier = notifier;
    }
}
