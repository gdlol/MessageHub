using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/key/{version}")]
[AllowAnonymous]
public class ServerKeysController : ControllerBase
{
    private readonly IIdentityService identityService;

    public ServerKeysController(IIdentityService identityService)
    {
        ArgumentNullException.ThrowIfNull(identityService);

        this.identityService = identityService;
    }

    [Route("server")]
    [Route("server/{keyId}")]
    [HttpGet]
    public object GetKeys()
    {
        if (identityService.HasSelfIdentity)
        {
            return identityService.GetSelfIdentity().GetServerKeys();
        }
        else
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
    }
}
