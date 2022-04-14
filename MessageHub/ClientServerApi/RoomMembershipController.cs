using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class RoomMembershipController : ControllerBase
{
    private readonly IRoomLoader roomLoader;

    public RoomMembershipController(IRoomLoader roomLoader)
    {
        ArgumentNullException.ThrowIfNull(roomLoader);

        this.roomLoader = roomLoader;
    }

    [Route("joined_rooms")]
    [HttpGet]
    public async Task<IActionResult> GetJoinedRooms()
    {
        var roomState = await roomLoader.LoadRoomStatesAsync(_ => true, includeLeave: false);
        return new JsonResult(new
        {
            joined_rooms = roomState.JoinedRoomIds.ToArray()
        });
    }
}
