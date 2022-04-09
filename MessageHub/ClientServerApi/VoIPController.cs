using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/v3/voip")]
public class VoIPController : ControllerBase
{
    [Route("turnServer")]
    [HttpGet]
    public object GetTurnServer()
    {
        return NotFound();
    }
}
