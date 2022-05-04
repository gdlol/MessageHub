using MessageHub.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/voip")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
public class VoIPController : ControllerBase
{
    [Route("turnServer")]
    [HttpGet]
    public object GetTurnServer()
    {
        return new
        {
            password = string.Empty,
            ttl = 86400,
            uris = Array.Empty<string>(),
            username = string.Empty
        };
    }
}
