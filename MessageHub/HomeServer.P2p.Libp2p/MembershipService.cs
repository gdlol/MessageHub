using System.Collections.Concurrent;

namespace MessageHub.HomeServer.P2p.Libp2p;

internal class MembershipService
{
    private readonly ILogger logger;
    private readonly IPeerIdentity peerIdentity;
    private readonly MemberStore memberStore;
    private readonly IPeerResolver peerResolver;
    private readonly Notifier<(string, string[])> notifier;
    private readonly EventHandler<(string, string[])> onNotify;

    public MembershipService(
        ILoggerFactory loggerFactory,
        IPeerIdentity peerIdentity,
        MemberStore memberStore,
        IPeerResolver peerResolver,
        Notifier<(string, string[])> notifier)
    {
        logger = loggerFactory.CreateLogger<MembershipService>();
        this.peerIdentity = peerIdentity;
        this.memberStore = memberStore;
        this.peerResolver = peerResolver;
        this.notifier = notifier;
        onNotify = (sender, e) =>
        {
            var (topic, ids) = e;
            UpdateMembers(topic, ids);
        };
    }

    public void Start()
    {
        notifier.OnNotify += onNotify;
    }

    public void Stop()
    {
        notifier.OnNotify -= onNotify;
    }

    public void UpdateMembers(string topic, IEnumerable<string> memberIds)
    {
        Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 8
            };
            var members = new ConcurrentBag<string>();
            await Parallel.ForEachAsync(memberIds, parallelOptions, async (id, token) =>
            {
                if (id == peerIdentity.Id)
                {
                    return;
                }
                try
                {
                    var addressInfo = await peerResolver.ResolveAddressInfoAsync(id, cancellationToken: token);
                    var peerId = Host.GetIdFromAddressInfo(addressInfo);
                    members.Add(peerId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error getting peer ID for {}", id);
                }
            });
            var existingMembers = memberStore.GetMembers(topic);
            var newMembers = members.Except(existingMembers);
            var oldMembers = existingMembers.Except(members);
            foreach (string member in newMembers)
            {
                memberStore.AddMember(topic, member);
            }
            foreach (string member in oldMembers)
            {
                memberStore.RemoveMember(topic, member);
            }
        });
    }
}
