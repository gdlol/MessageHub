using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}")]
public class FilterController : ControllerBase
{
    private readonly IAccountData accountData;

    public FilterController(IAccountData accountData)
    {
        ArgumentNullException.ThrowIfNull(accountData);

        this.accountData = accountData;
    }

    [Route("user/{userId}/filter")]
    [HttpPost]
    public async Task<object> Filter(string userId, [FromBody] Filter filter)
    {
        if (userId is null || filter is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }

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
            filter_id = await accountData.SaveFilterAsync(filterJson)
        };
    }

    [Route("user/{userId}/filter/{filterId}")]
    [HttpGet]
    public async Task<object> Filter(string userId, string filterId)
    {
        if (userId is null || filterId is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.InvalidParameter));
        }

        var identity = Request.HttpContext.User.Identity!;
        if (userId != identity.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        string? filter = await accountData.LoadFilterAsync(filterId);
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
