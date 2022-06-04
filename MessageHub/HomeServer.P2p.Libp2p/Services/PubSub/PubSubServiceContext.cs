using System.Collections.Concurrent;
using MessageHub.HomeServer.P2p.Libp2p.Notifiers;
using MessageHub.HomeServer.P2p.Notifiers;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace MessageHub.HomeServer.P2p.Libp2p.Services.PubSub;

internal class PubSubServiceContext
{
    private readonly IServer server;

    public ILoggerFactory LoggerFactory { get; }
    public IIdentityService IdentityService { get; }
    public IRooms Rooms { get; }
    public IHttpClientFactory HttpClientFactory { get; }
    public string SelfUrl => server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
    public MembershipUpdateNotifier MembershipUpdateNotifier { get; }
    public PublishEventNotifier PublishEventNotifier { get; }
    public RemoteRequestNotifier RemoteRequestNotifier { get; }
    public ConcurrentDictionary<string, (Topic, CancellationTokenSource)> JoinedTopics { get; }

    public PubSubServiceContext(
        IServer server,
        ILoggerFactory loggerFactory,
        IIdentityService identityService,
        IRooms rooms,
        IHttpClientFactory httpClientFactory,
        MembershipUpdateNotifier membershipUpdateNotifier,
        PublishEventNotifier publishEventNotifier,
        RemoteRequestNotifier remoteRequestNotifier)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(membershipUpdateNotifier);
        ArgumentNullException.ThrowIfNull(publishEventNotifier);
        ArgumentNullException.ThrowIfNull(remoteRequestNotifier);

        this.server = server;
        LoggerFactory = loggerFactory;
        IdentityService = identityService;
        Rooms = rooms;
        HttpClientFactory = httpClientFactory;
        MembershipUpdateNotifier = membershipUpdateNotifier;
        PublishEventNotifier = publishEventNotifier;
        RemoteRequestNotifier = remoteRequestNotifier;
        JoinedTopics = new ConcurrentDictionary<string, (Topic, CancellationTokenSource)>();
    }
}
