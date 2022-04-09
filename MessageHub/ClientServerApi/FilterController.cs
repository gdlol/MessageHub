using System.Text.Json;
using MessageHub.ClientServerProtocol;
using MessageHub.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServerApi;

[Route("_matrix/client/{version}")]
public class FilterController : ControllerBase
{
    private readonly IMatrixPersistenceService persistence;

    public FilterController(IMatrixPersistenceService persistence)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        this.persistence = persistence;
    }

    [Route("user/{userId}/filter")]
    [HttpPost]
    public async Task<object> Filter(string userId, [FromBody] JsonElement filter)
    {
        var identity = Request.HttpContext.User.Identity;
        if (identity is null || userId != identity.Name)
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }

        return new
        {
            filter_id = await persistence.SaveFilterAsync(userId, filter.ToString())
        };
    }

    [Route("user/{userId}/filter/{filterId}")]
    [HttpGet]
    public async Task<object> Filter(string userId, string filterId)
    {
        var identity = Request.HttpContext.User.Identity;
        if (identity is null || userId != identity.Name)
        {
            return Unauthorized(MatrixError.Create(MatrixErrorCode.Unauthorized));
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
