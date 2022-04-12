using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.ClientServerApi.Sync;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class SyncController : ControllerBase
{
    private readonly FilterLoader filterLoader;
    private readonly AccountDataLoader accountDataLoader;
    private readonly RoomLoader roomLoader;

    public SyncController(IPersistenceService persistenceService, IRoomLoader roomLoader)
    {
        ArgumentNullException.ThrowIfNull(persistenceService);
        ArgumentNullException.ThrowIfNull(roomLoader);

        filterLoader = new FilterLoader(persistenceService);
        accountDataLoader = new AccountDataLoader(persistenceService);
        this.roomLoader = new RoomLoader(roomLoader, accountDataLoader);
    }

    [Route("sync")]
    [HttpGet]
    public async Task<IActionResult> Sync(SyncParmeters parmeters)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }

        var (filter, error) = await filterLoader.LoadFilterAsync(parmeters.Filter);
        if (error is not null)
        {
            return BadRequest(error);
        }
        var accountData = await accountDataLoader.LoadAccountDataAsync(userId, filter?.AccountData);
        var (nextBatch, rooms) = await roomLoader.LoadRoomsAsync(
            userId,
            parmeters.FullState,
            parmeters.Since,
            filter?.Room);
        return new JsonResult(new SyncResponse
        {
            AccountData = accountData,
            NextBatch = nextBatch,
            Rooms = rooms
        },
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}
