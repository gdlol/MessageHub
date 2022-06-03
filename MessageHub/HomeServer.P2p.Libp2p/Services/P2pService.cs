using BackgroundService = MessageHub.HomeServer.Services.BackgroundService;

namespace MessageHub.HomeServer.P2p.Libp2p.Services;

internal interface IP2pService
{
    BackgroundService Create(P2pNode p2pNode);
}
