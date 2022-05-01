using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Client;

[Route("_matrix/client/{version}")]
public class RoomMembershipController : ControllerBase
{
    private readonly ITimelineLoader timelineLoader;

    public RoomMembershipController(ITimelineLoader timelineLoader)
    {
        ArgumentNullException.ThrowIfNull(timelineLoader);

        this.timelineLoader = timelineLoader;
    }

    [Route("joined_rooms")]
    [HttpGet]
    public async Task<IActionResult> GetJoinedRooms()
    {
        var roomState = await timelineLoader.LoadRoomStatesAsync(_ => true, includeLeave: false);
        return new JsonResult(new
        {
            joined_rooms = roomState.JoinedRoomIds.ToArray()
        });
    }
}
