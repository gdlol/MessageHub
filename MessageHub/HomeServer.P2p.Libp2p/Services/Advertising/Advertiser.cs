namespace MessageHub.HomeServer.P2p.Libp2p.Services.Advertising;

internal class Advertiser
{
    private readonly ILogger logger;
    private readonly AdvertisingServiceContext context;
    private readonly P2pNode p2pNode;

    public Advertiser(ILogger logger, AdvertisingServiceContext context, P2pNode p2pNode)
    {
        this.logger = logger;
        this.context = context;
        this.p2pNode = p2pNode;
    }

    private async Task Advertise(string topic, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                logger.LogDebug("Advertising topic: {}", topic);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                p2pNode.Discovery.Advertise(topic, cts.Token);
                return;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation("Timeout advertising topic {}: {}", topic, ex.Message);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                string selfId = p2pNode.Host.Id;
                var peers = p2pNode.Discovery.FindPeers(topic, cts.Token);
                int count = 0;
                foreach (var (peerId, _) in peers)
                {
                    if (peerId == selfId)
                    {
                        logger.LogInformation("Already advertising topic {}", topic);
                        return;
                    }
                    count++;
                }
                logger.LogInformation("Found {} nodes advertising topic {}", topic);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    public async Task AdvertiseAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Advertising discovery points...");
        try
        {
            var identity = context.IdentityService.GetSelfIdentity();
            stoppingToken.ThrowIfCancellationRequested();
            await Advertise($"p2p:{identity.Id}", stoppingToken);
            var userId = UserIdentifier.FromId(identity.Id);
            string displayName = await context.UserProfile.GetDisplayNameAsync(userId.ToString())
                ?? userId.UserName;
            for (int i = 7; i < 11; i++)
            {
                string peerIdSuffix = identity.Id[^i..];
                string rendezvousPoint = $"{displayName}:{peerIdSuffix}";
                await Advertise(rendezvousPoint, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error advertising discovery points.");
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
