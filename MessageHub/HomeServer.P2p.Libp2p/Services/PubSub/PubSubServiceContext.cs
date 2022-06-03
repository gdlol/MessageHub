using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Notifiers;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;

internal class PubSubServiceContext
{
    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public MembershipUpdateNotifier MembershipUpdateNotifier { get; }
    public PublishEventNotifier PublishEventNotifier { get; }
    public RemoteRequestNotifier RemoteRequestNotifier { get; }
    public ConcurrentDictionary<string, (Topic, CancellationTokenSource)> JoinedTopics { get; }

    public PubSubServiceContext(
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        MembershipUpdateNotifier membershipUpdateNotifier,
        PublishEventNotifier publishEventNotifier,
        RemoteRequestNotifier remoteRequestNotifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(remoteRequestNotifier);

        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        MembershipUpdateNotifier = membershipUpdateNotifier;
        PublishEventNotifier = publishEventNotifier;
        RemoteRequestNotifier = remoteRequestNotifier;
        JoinedTopics = new ConcurrentDictionary<string, (Topic, CancellationTokenSource)>();
    }
}
