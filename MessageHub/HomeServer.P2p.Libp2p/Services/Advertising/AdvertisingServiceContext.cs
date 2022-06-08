using MessageHub.HomeServer.Notifiers;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class AdvertisingServiceContext
{
    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public IUserProfile UserProfile { get; }
    public UserProfileUpdateNotifier Notifier { get; }

    public AdvertisingServiceContext(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        IUserProfile userProfile,
        UserProfileUpdateNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(notifier);

        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        UserProfile = userProfile;
        Notifier = notifier;
    }
}
