using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class CapabilitiesController : ControllerBase
{
    [Route("capabilities")]
    [HttpGet]
    public object GetCapabilities()
    {
        return new
        {
            capabilities = new Dictionary<string, object>
            {
                ["m.change_password"] = new { enabled = false },
                ["m.room_versions"] = new Dictionary<string, object>
                {
                    ["default"] = 1,
                    ["available"] = new Dictionary<string, object>
                    {
                        ["1"] = "stable"
                    }
                }
                ["m.set_displayname"] = new { enabled = true },
                ["m.set_avatar_url"] = new { enabled = true },
                ["m.3pid_changes"] = new { enabled = false }
            }
        };
    }
}
