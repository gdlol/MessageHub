using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Client;

[Route("_matrix/client/{version}/voip")]
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
