using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/key/{version}")]
[AllowAnonymous]
public class ServerKeysController : ControllerBase
{
    private readonly IPeerIdentity peerIdentity;

    public ServerKeysController(IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);

        this.peerIdentity = peerIdentity;
    }

    [Route("server")]
    [Route("server/{keyId}")]
    [HttpGet]
    public object GetKeys()
    {
        return peerIdentity.GetServerKeys();
    }
}
