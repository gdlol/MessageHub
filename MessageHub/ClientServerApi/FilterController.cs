using System.Text.Json;
using MessageHub.ClientServerProtocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class FilterController : ControllerBase
{
    private readonly IPersistenceService persistence;

    public FilterController(IPersistenceService persistence)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        this.persistence = persistence;
    }

    [Route("user/{userId}/filter")]
    [HttpPost]
    public async Task<object> Filter(string userId, [FromBody] Filter filter)
    {
        var identity = Request.HttpContext.User.Identity!;
        if (userId != identity.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        string filterJson = JsonSerializer.Serialize(filter);
        return new
        {
            filter_id = await persistence.SaveFilterAsync(userId, filterJson)
        };
    }

    [Route("user/{userId}/filter/{filterId}")]
    [HttpGet]
    public async Task<object> Filter(string userId, string filterId)
    {
        var identity = Request.HttpContext.User.Identity!;
        if (userId != identity.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        string? filter = await persistence.LoadFilterAsync(userId, filterId);
        if (filter is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return JsonSerializer.Deserialize<JsonElement>(filter);
        }
    }
}
