using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("/_matrix/client/{version}/")]
public class PresenceController : ControllerBase
{
    [Route("presence/{userId}/status")]
    [HttpGet]
    public object GetStatus()
    {
        return new
        {
            presence = "online"
        };
    }

    [Route("presence/{userId}/status")]
    [HttpPut]
    public object SetStatus()
    {
        return new object();
    }
}
