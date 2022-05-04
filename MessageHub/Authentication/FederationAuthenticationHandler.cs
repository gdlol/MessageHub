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
    private readonly IPeerStore peerStore;
    private readonly IRooms rooms;

    public FederationAuthenticationHandler(
        IOptionsMonitor<FederationAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IPeerIdentity identity,
        IPeerStore peerStore,
        IRooms rooms) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peerStore);
        ArgumentNullException.ThrowIfNull(rooms);

        this.identity = identity;
        this.peerStore = peerStore;
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
                        originSignatures[keyIdentifier.ToString()] = signature;
                    }
                }
            }
        }
        string? sender = signatures.Keys.SingleOrDefault();
        if (sender is null)
        {
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
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
        catch (Exception)
        {
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
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        if (!Request.Headers.TryGetValue("Matrix-Timestamp", out var timestampValues)
            || !long.TryParse(timestampValues.SingleOrDefault(), out long timestamp))
        {
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
            Signatures = JsonSerializer.SerializeToElement(signatures)
        };
        if (request.Destination != identity.Id && !rooms.HasRoom(request.Destination))
        {
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
        if (peerStore.TryGetPeer(sender, out var peer)
            && identity.VerifyJson(peer, JsonSerializer.SerializeToElement(request, ignoreNullOptions)))
        {
            Request.HttpContext.Items[nameof(request)] = request;
            var claims = new[] { new Claim(ClaimTypes.Name, peer.Id) };
            var claimsIdentity = new ClaimsIdentity(claims, MatrixAuthenticationSchemes.Federation);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        else
        {
            var error = MatrixError.Create(MatrixErrorCode.Unauthorized);
            return AuthenticateResult.Fail(error.ToString());
        }
    }
}
