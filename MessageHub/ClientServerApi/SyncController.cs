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
    public async Task<IActionResult> Sync(SyncParameters parameters)
    {
        string? userId = Request.HttpContext.User.Identity?.Name;
        if (userId is null)
        {
            throw new InvalidOperationException();
        }
        if (parameters is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.MissingParameter));
        }

        var (filter, error) = await filterLoader.LoadFilterAsync(parameters.Filter);
        if (error is not null)
        {
            return BadRequest(error);
        }
        if (parameters.Timeout > 0)
        {
            using var cts = new CancellationTokenSource();
            using var timer = new Timer(
                _ => cts.Cancel(),
                null,
                dueTime: Math.Min(parameters.Timeout, TimeSpan.FromMinutes(10).Milliseconds),
                period: Timeout.Infinite);
            while (!cts.IsCancellationRequested)
            {
                if (roomLoader.CurrentBatchId != (parameters.Since ?? string.Empty))
                {
                    break;
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
                catch (OperationCanceledException)
                { }
            }
        }
        var accountData = await accountDataLoader.LoadAccountDataAsync(userId, filter?.AccountData);
        var (nextBatch, rooms) = await roomLoader.LoadRoomsAsync(
            userId,
            parameters.FullState,
            parameters.Since,
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
