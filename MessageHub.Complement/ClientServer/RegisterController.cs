using System.Text.RegularExpressions;
using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.ClientServer.Protocol;
using MessageHub.Complement.HomeServer;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}")]
public class RegisterController : ControllerBase
{
    private readonly ILogger logger;
    private readonly Config config;
    private readonly IUserRegistration userRegistration;
    private readonly IUserLogIn userLogIn;

    public RegisterController(
        ILogger<RegisterController> logger,
        Config config,
        IUserRegistration userRegistration,
        IUserLogIn userLogIn)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(userRegistration);
        ArgumentNullException.ThrowIfNull(userLogIn);

        this.logger = logger;
        this.config = config;
        this.userRegistration = userRegistration;
        this.userLogIn = userLogIn;
    }

    [Route("register")]
    [HttpPost]
    [MiddlewareFilter(typeof(FillJsonContentTypePipeline))]
    public async Task<object> Register(
        [FromQuery] string? kind,
        [FromBody, ValidateNever] RegisterRequest request)
    {
        if (!ModelState.IsValid || request.AuthenticationData is null)
        {
            return ApplicationResults.Json(new AuthenticationResponse
            {
                Flows = new[]
                {
                    new FlowInformation
                    {
                        Stages = new[] { LogInTypes.Password }
                    }
                }
            }, StatusCodes.Status401Unauthorized);
        }

        kind ??= "user";
        if (kind != "user")
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden, $"{nameof(kind)}: {kind}"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        string deviceId = request.DeviceId ?? Guid.NewGuid().ToString();

        string userName = request.Username?.ToLowerInvariant() ?? Guid.NewGuid().ToString();
        if (userName == string.Empty || !Regex.IsMatch(userName, "^[a-z0-9._=/-]+$"))
        {
            var error = MatrixError.Create(MatrixErrorCode.InvalidUserName, $"{nameof(userName)}: {userName}");
            return BadRequest(error);
        }

        if (request.Password is null)
        {
            var error = MatrixError.Create(MatrixErrorCode.MissingParameter, nameof(request.Password));
            return BadRequest(error);
        }

        bool registered = await userRegistration.TryRegisterAsync(userName, request.Password);
        if (!registered)
        {
            var error = MatrixError.Create(MatrixErrorCode.UserInUse, $"{nameof(userName)}: {userName}");
            return BadRequest(error);
        }

        string userId = $"@{userName}:{config.ServerName}";
        logger.LogDebug("Registered user {}", userId);

        if (request.InhibitLogin == true)
        {
            return new RegisterResponse
            {
                UserId = userId
            };
        }

        logger.LogDebug("Login user {}...", userName);
        var loginResponse = await userLogIn.LogInAsync(userName, deviceId);

        return new RegisterResponse
        {
            AccessToken = loginResponse.AccessToken,
            DeviceId = loginResponse.DeviceId,
            ExpiresInMillisecond = loginResponse.ExpiresInMillisecond,
            RefreshToken = loginResponse.RefreshToken,
            UserId = userId
        };
    }
}
