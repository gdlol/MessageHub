using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/client/{version}/user")]
public class AccountDataController : ControllerBase
{
    private readonly IAccountData accountData;

    public AccountDataController(IAccountData accountData)
    {
        ArgumentNullException.ThrowIfNull(accountData);

        this.accountData = accountData;
    }

    [Route("{userId}/account_data/{type}")]
    [HttpGet]
    public async Task<IActionResult> GetAccountData(string userId, string type)
    {
        if (userId != Request.HttpContext.User.Identity?.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        var result = await accountData.LoadAccountDataAsync(null, type);
        if (result is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return new JsonResult(result);
        }
    }

    [Route("{userId}/account_data/{type}")]
    [HttpPut]
    public async Task<IActionResult> SetAccountData(
        [FromRoute] string userId,
        [FromRoute] string type,
        [FromBody] JsonElement? body)
    {
        if (userId != Request.HttpContext.User.Identity?.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        await accountData.SaveAccountDataAsync(null, type, body);
        return new JsonResult(new object());
    }

    [Route("{userId}/rooms/{roomId}/account_data/{type}")]
    [HttpGet]
    public async Task<IActionResult> GetAccountData(string userId, string roomId, string type)
    {
        if (userId != Request.HttpContext.User.Identity?.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        var result = await accountData.LoadAccountDataAsync(roomId, type);
        if (result is null)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return new JsonResult(result);
        }
    }

    [Route("{userId}/rooms/{roomId}/account_data/{type}")]
    [HttpPut]
    public async Task<IActionResult> SetAccountData(
        [FromRoute] string userId,
        [FromRoute] string roomId,
        [FromRoute] string type,
        [FromBody] JsonElement? body)
    {
        if (userId != Request.HttpContext.User.Identity?.Name)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        await accountData.SaveAccountDataAsync(roomId, type, body);
        return new JsonResult(new object());
    }
}
