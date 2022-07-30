using System.Collections.Immutable;
using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.ClientServer.Protocol;
using MessageHub.Complement.HomeServer;
using MessageHub.HomeServer;
using MessageHub.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}")]
public class LogInController : ControllerBase
{
    private readonly Config config;
    private readonly IUserLogIn userLogIn;
    private readonly IUserRegistration userRegistration;

    public LogInController(
        Config config,
        IUserRegistration userRegistration,
        IUserLogIn userLogIn)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(userRegistration);
        ArgumentNullException.ThrowIfNull(userLogIn);

        this.config = config;
        this.userRegistration = userRegistration;
        this.userLogIn = userLogIn;
    }

    [Route("login")]
    [HttpGet]
    public IActionResult LogIn() => ApplicationResults.Json(new
    {
        flows = new object[]
        {
            new
            {
                type = LogInTypes.Password
            }
        }
    });

    [Route("login")]
    [HttpPost]
    public async Task<object> LogIn([FromBody] Protocol.LogInRequest request)
    {
        if (request.LogInType != LogInTypes.Password)
        {
            var error = MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(request.LogInType)}: {request.LogInType}");
            return BadRequest(error);
        }
        if (request.Identifier is null)
        {
            var error = MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(request.Identifier));
            return BadRequest(error);
        }
        if (request.Password is null)
        {
            var error = MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(request.Password));
            return BadRequest(error);
        }

        string userName = request.Identifier.User;
        if (UserIdentifier.TryParse(userName, out var identifier))
        {
            if (identifier.Id != config.ServerName)
            {
                var error = MatrixError.Create(MatrixErrorCode.InvalidUserName);
                return BadRequest(error);
            }
            userName = identifier.UserName;
        }
        userName = userName.ToLowerInvariant();

        if (!await userRegistration.VerifyUserAsync(userName, request.Password))
        {
            var error = MatrixError.Create(MatrixErrorCode.Forbidden);
            return new JsonResult(error)
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var loginResponse = await userLogIn.LogInAsync(userName, request.DeviceId, request.InitialDeviceDisplayName);
        loginResponse = loginResponse with
        {
            UserId = $"@{userName}:{config.ServerName}"
        };

        // Add home_server field.
        return DefaultJsonSerializer
            .SerializeToElement(loginResponse)
            .Deserialize<ImmutableDictionary<string, object>>()!
            .Add("home_server", config.ServerName);
    }
}
