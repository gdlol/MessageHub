using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Notifiers;
using MessageHub.HomeServer.Rooms;
using MessageHub.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class FederationAuthenticationHandler : AuthenticationHandler<FederationAuthenticationSchemeOptions>
{
    private readonly AuthenticatedRequestNotifier notifier;
    private readonly IIdentityService identityService;
    private readonly IRooms rooms;

    public FederationAuthenticationHandler(
        IOptionsMonitor<FederationAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        AuthenticatedRequestNotifier notifier,
        IIdentityService identityService,
        IRooms rooms) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(rooms);

        this.notifier = notifier;
        this.identityService = identityService;
        this.rooms = rooms;
    }

    private static bool TryParseAuthorizationHeader(string s, out Dictionary<string, string> header)
    {
        header = new Dictionary<string, string>();
        foreach (string item in s.Split(','))
        {
            var pair = item.Split('=', 2);
            if (pair.Length != 2)
            {
                return false;
            }
            var (key, value) = (pair[0], pair[1]);
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }
            header[key] = value;
        }
        return true;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AuthenticateResult Fail(MatrixError error)
        {
            Request.HttpContext.SetMatrixError(error);
            return AuthenticateResult.Fail(error.ToString());
        }

        if (!identityService.HasSelfIdentity)
        {
            Logger.LogDebug("Self identity not initialized.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        var identity = identityService.GetSelfIdentity();

        Signatures signatures = new();
        string? destination = null;
        if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            foreach (string value in authorizationHeader)
            {
                if (AuthenticationHeaderValue.TryParse(value, out var header) && header.Scheme == "X-Matrix")
                {
                    if (header.Parameter is null
                        || !TryParseAuthorizationHeader(header.Parameter, out var headerValues))
                    {
                        continue;
                    }
                    if (headerValues.TryGetValue("origin", out var origin)
                        && headerValues.TryGetValue("destination", out destination)
                        && headerValues.TryGetValue("key", out var keyIdentifierString)
                        && KeyIdentifier.TryParse(keyIdentifierString, out var keyIdentifier)
                        && headerValues.TryGetValue("sig", out var signature))
                    {
                        if (!signatures.TryGetValue(origin, out var originSignatures))
                        {
                            signatures[origin] = originSignatures = new ServerSignatures();
                        }
                        originSignatures[keyIdentifier] = signature;
                    }
                }
            }
        }
        string? sender = signatures.Keys.SingleOrDefault();
        if (sender is null)
        {
            Logger.LogInformation("Sender signature not found.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        if (destination is null)
        {
            Logger.LogInformation("Destination not found.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }

        Request.EnableBuffering();
        JsonElement? content = null;
        try
        {
            using var stream = new MemoryStream();
            await Request.Body.CopyToAsync(stream);
            var requestBody = Encoding.UTF8.GetString(stream.ToArray());
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                content = JsonSerializer.Deserialize<JsonElement>(requestBody);
            }
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Error authenticating request.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unknown));
        }
        finally
        {
            Request.Body.Position = 0;
        }
        if (!Request.Headers.TryGetValue("Matrix-Timestamp", out var timestampValues)
            || !long.TryParse(timestampValues.SingleOrDefault(), out long timestamp))
        {
            Logger.LogInformation("Matrix-Timestamp not found.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        if (!Request.Headers.TryGetValue("Matrix-ServerKeys", out var serverKeyValues)
            || serverKeyValues.SingleOrDefault() is not string serverKeysString)
        {
            Logger.LogInformation("Matrix-ServerKeys not found.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        ServerKeys? serverKeys = null;
        try
        {
            var bytes = Convert.FromHexString(serverKeysString);
            serverKeys = JsonSerializer.Deserialize<ServerKeys>(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Error parsing Matrix-ServerKeys");
        }
        if (serverKeys is null)
        {
            Logger.LogInformation("Invalid Matrix-ServerKeys.");
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        var request = new SignedRequest
        {
            Method = Request.Method.ToUpperInvariant(),
            Uri = UriHelper.BuildRelative(path: Request.Path, query: Request.QueryString),
            Origin = sender,
            OriginServerTimestamp = timestamp,
            Destination = destination,
            Content = content,
            ServerKeys = serverKeys,
            Signatures = DefaultJsonSerializer.SerializeToElement(signatures)
        };
        if (request.Destination != identity.Id && !rooms.HasRoom(request.Destination))
        {
            Logger.LogInformation("Invalid destination: {}", request.Destination);
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
        var requestElement = request.ToJsonElement();
        if (identityService.VerifyJson(sender, requestElement))
        {
            Request.HttpContext.SetSignedRequest(request);
            var claims = new[] { new Claim(ClaimTypes.Name, sender) };
            var claimsIdentity = new ClaimsIdentity(claims, MatrixAuthenticationSchemes.Federation);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            notifier.Notify(serverKeys);
            return AuthenticateResult.Success(ticket);
        }
        else
        {
            Logger.LogInformation("VerifyJson failed for {}: {}", sender, requestElement);
            return Fail(MatrixError.Create(MatrixErrorCode.Unauthorized));
        }
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await base.HandleChallengeAsync(properties);
        if (Request.HttpContext.TryGetMatrixError(out var error))
        {
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(error);
        }
    }
}
