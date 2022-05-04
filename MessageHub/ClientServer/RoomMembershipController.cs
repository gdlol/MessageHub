using MessageHub.Authentication;
using MessageHub.HomeServer.Rooms.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
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
        var roomState = await timelineLoader.LoadBatchStatesAsync(_ => true, includeLeave: false);
        return new JsonResult(new
        {
            joined_rooms = roomState.JoinedRoomIds.ToArray()
        });
    }
}
