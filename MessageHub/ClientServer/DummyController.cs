using MessageHub.ClientServer.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class DummyController : ControllerBase
{
    [Route("thirdparty/protocols")]
    [HttpGet]
    public object GetThirdPartyProtocols()
    {
        return new object();
    }

    [Route("keys/upload")]
    [HttpPost]
    public object UploadKeys()
    {
        return new object();
    }

    [Route("keys/query")]
    [HttpPost]
    public object QueryKeys()
    {
        return new
        {
            device_keys = new object(),
        };
    }

    [Route("room_keys/version")]
    [HttpGet]
    public object GetRoomKeys()
    {
        return new JsonResult(MatrixError.Create(MatrixErrorCode.NotFound))
        {
            StatusCode = StatusCodes.Status404NotFound
        };
    }

    [Route("pushrules")]
    [HttpGet]
    public object GetPushRules()
    {
        return new
        {
            global = new object()
        };
    }

    [Route("rooms/{roomId}/typing/{userId}")]
    [HttpPut]
    public object SetTyping()
    {
        return new object();
    }
}
