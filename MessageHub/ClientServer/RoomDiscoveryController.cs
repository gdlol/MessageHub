using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class RoomDiscoveryController : ControllerBase
{
    private readonly IRoomDiscoveryService roomDiscoveryService;

    public RoomDiscoveryController(IRoomDiscoveryService roomDiscoveryService)
    {
        ArgumentNullException.ThrowIfNull(roomDiscoveryService);

        this.roomDiscoveryService = roomDiscoveryService;
    }

    [Route("directory/room/{roomAlias}")]
    [HttpGet]
    public async Task<IActionResult> GetAliasAsync(string roomAlias)
    {
        string? roomId = await roomDiscoveryService.GetRoomIdAsync(roomAlias);
        if (roomId is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        var servers = await roomDiscoveryService.GetServersAsync(roomId);
        return new JsonResult(new
        {
            room_id = roomId,
            servers
        });
    }

    [Route("directory/room/{roomAlias}")]
    [HttpPut]
    public async Task<IActionResult> SetAlias([FromRoute] string roomAlias, [FromBody] SetAliasParameters parameters)
    {
        bool? result = await roomDiscoveryService.SetRoomAliasAsync(parameters.RoomId, roomAlias);
        if (result is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            if (result.Value)
            {
                return new JsonResult(new object());
            }
            else
            {
                return Conflict(MatrixError.Create(MatrixErrorCode.Unknown));
            }
        }
    }

    [Route("directory/room/{roomAlias}")]
    [HttpDelete]
    public async Task<IActionResult> DeleteAlias([FromRoute] string roomAlias)
    {
        bool deleted = await roomDiscoveryService.DeleteRoomAliasAsync(roomAlias);
        if (deleted)
        {
            return new JsonResult(new object());
        }
        else
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
    }

    [Route("rooms/{roomId}/aliases")]
    [HttpGet]
    public async Task<IActionResult> GetAliases([FromRoute] string roomId)
    {
        var aliases = await roomDiscoveryService.GetAliasesAsync(roomId);
        return new JsonResult(new { aliases });
    }
}
