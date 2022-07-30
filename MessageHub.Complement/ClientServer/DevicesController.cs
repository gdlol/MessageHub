using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.Complement.Authentication;
using MessageHub.Complement.ClientServer.Protocol;
using MessageHub.Complement.HomeServer;
using MessageHub.Complement.ReverseProxy;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MessageHub.Complement.ClientServer;

[Route("_matrix/client/{version}")]
[Authorize(AuthenticationSchemes = ComplementAuthenticationSchemes.Complement)]
public class DevicesController : ControllerBase
{
    private readonly Config config;
    private readonly HomeServerHttpForwarder forwarder;
    private readonly IUserRegistration userRegistration;

    public DevicesController(Config config, HomeServerHttpForwarder forwarder, IUserRegistration userRegistration)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(forwarder);
        ArgumentNullException.ThrowIfNull(userRegistration);

        this.config = config;
        this.forwarder = forwarder;
        this.userRegistration = userRegistration;
    }

    [Route("devices")]
    [HttpGet]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public void Devices() => throw new InvalidOperationException();

    [Route("devices/{deviceId}")]
    [HttpGet]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public void GetDevice() => throw new InvalidOperationException();

    [Route("devices/{deviceId}")]
    [HttpPut]
    [MiddlewareFilter(typeof(P2pServerProxyPipeline))]
    public void SetDevice() => throw new InvalidOperationException();

    private async Task<IActionResult> DeleteAsync<TRequest>(TRequest request, AuthenticationData? authenticationData)
    {
        MatrixError? error = null;
        if (!ModelState.IsValid || authenticationData?.Type != LogInTypes.Password)
        {
            error = MatrixError.Create(MatrixErrorCode.Unauthorized);
        }
        else if (authenticationData.Identifier?.User is not string authUserId
            || !UserIdentifier.TryParse(authUserId, out var authUserIdentifier)
            || authUserIdentifier.Id != config.ServerName
            || authenticationData.Password is not string authPassword
            || !await userRegistration.VerifyUserAsync(authUserIdentifier.UserName, authPassword))
        {
            error = MatrixError.Create(MatrixErrorCode.Forbidden);
        }
        if (error is not null)
        {
            return ApplicationResults.Json(new AuthenticationResponse
            {
                ErrorCode = error.ErrorCode,
                Error = error.Error,
                Flows = new[]
                {
                    new FlowInformation
                    {
                        Stages = new[] { LogInTypes.Password }
                    }
                },
                Parameters = JsonSerializer.SerializeToElement(new object()),
                Session = string.Empty
            }, StatusCodes.Status401Unauthorized);
        }
        string userName = HttpContext.User.Identity?.Name ?? throw new InvalidOperationException();
        string userId = $"@{userName}:{config.ServerName}";
        if (userId != authenticationData!.Identifier!.User)
        {
            return new JsonResult(MatrixError.Create(MatrixErrorCode.Forbidden))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        string? serverAddress = await userRegistration.TryGetAddressAsync(userName);
        _ = serverAddress ?? throw new InvalidOperationException();
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);
        Request.Body = new MemoryStream(requestBytes);
        Request.ContentLength = requestBytes.Length;
        await forwarder.SendAsync(HttpContext, serverAddress);
        return new EmptyResult();
    }

    [Route("devices/{deviceId}")]
    [HttpDelete]
    [MiddlewareFilter(typeof(FillNullBodyPipeline))]
    public Task<IActionResult> DeleteDevice(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), ValidateNever] DeleteDeviceRequest? request)
    {
        return DeleteAsync(request, request?.AuthenticationData);
    }

    [Route("delete_devices")]
    [HttpPost]
    [MiddlewareFilter(typeof(FillNullBodyPipeline))]
    public Task<IActionResult> DeleteDevices(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), ValidateNever] Protocol.DeleteDevicesRequest request)
    {
        return DeleteAsync(request, request?.AuthenticationData);
    }
}
