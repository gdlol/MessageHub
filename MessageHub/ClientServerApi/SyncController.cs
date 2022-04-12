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
    private readonly IPersistenceService persistenceService;
    private readonly FilterLoader filterLoader;
    private readonly AccountDataLoader accountDataLoader;

    public SyncController(IPersistenceService persistenceService)
    {
        ArgumentNullException.ThrowIfNull(persistenceService);

        this.persistenceService = persistenceService;
        filterLoader = new FilterLoader(persistenceService);
        accountDataLoader = new AccountDataLoader(persistenceService);
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
        var accountData = await accountDataLoader.GetAccountDataAsync(userId, filter?.AccountData);
        return new JsonResult(new SyncResponse
        {
            AccountData = accountData
        },
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}
