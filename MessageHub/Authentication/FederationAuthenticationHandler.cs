using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using MessageHub.HomeServer.Rooms;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class FederationAuthenticationHandler : AuthenticationHandler<FederationAuthenticationSchemeOptions>
{
    private static readonly JsonSerializerOptions ignoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPeerIdentity identity;
    private readonly IRooms rooms;

    public FederationAuthenticationHandler(
        IOptionsMonitor<FederationAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IPeerIdentity identity,
        IRooms rooms) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(rooms);

        this.identity = identity;
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
        Signatures signatures = new();
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
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            Logger.LogDebug("Sender signature not found.");
            return AuthenticateResult.Fail(error.ToString());
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
            Logger.LogDebug(ex, "Error authenticating request.");
            var error = MatrixError.Create(MatrixErrorCode.Unknown);
            return AuthenticateResult.Fail(error.ToString());
        }
        finally
        {
            Request.Body.Position = 0;
        }
        if (!Request.Headers.TryGetValue("Matrix-Host", out var hostValues)
            || hostValues.SingleOrDefault() is not string host)
        {
            Logger.LogDebug("Matrix-Host not found.");
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        if (!Request.Headers.TryGetValue("Matrix-Timestamp", out var timestampValues)
            || !long.TryParse(timestampValues.SingleOrDefault(), out long timestamp))
        {
            Logger.LogDebug("Matrix-Timestamp not found.");
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        if (!Request.Headers.TryGetValue("Matrix-ServerKeys", out var serverKeyValues)
            || serverKeyValues.SingleOrDefault() is not string serverKeysString)
        {
            Logger.LogDebug("Matrix-ServerKeys not found.");
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        ServerKeys? serverKeys = null;
        try
        {
            var bytes = Convert.FromHexString(serverKeysString);
            serverKeys = JsonSerializer.Deserialize<ServerKeys>(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error parsing Matrix-ServerKeys");
        }
        if (serverKeys is null)
        {
            Logger.LogDebug("Invalid Matrix-ServerKeys.");
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        var request = new SignedRequest
        {
            Method = Request.Method.ToUpperInvariant(),
            Uri = UriHelper.BuildRelative(path: Request.Path, query: Request.QueryString),
            Origin = sender,
            OriginServerTimestamp = timestamp,
            Destination = host,
            Content = content,
            ServerKeys = serverKeys,
            Signatures = JsonSerializer.SerializeToElement(signatures)
        };
        if (request.Destination != identity.Id && !rooms.HasRoom(request.Destination))
        {
            Logger.LogDebug("Invalid destination: {}", request.Destination);
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        var requestElement = JsonSerializer.SerializeToElement(request, ignoreNullOptions);
        if (identity.VerifyJson(sender, requestElement))
        {
            Request.HttpContext.Items[nameof(request)] = request;
            var claims = new[] { new Claim(ClaimTypes.Name, sender) };
            var claimsIdentity = new ClaimsIdentity(claims, MatrixAuthenticationSchemes.Federation);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        else
        {
            Logger.LogDebug("VerifyJson failed for {}: {}", sender, requestElement);
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
    }
}
