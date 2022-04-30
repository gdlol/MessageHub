using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MessageHub.ClientServer.Protocol;
using MessageHub.Federation;
using MessageHub.Federation.Protocol;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MessageHub.Authentication;

public class FederationAuthenticationHandler : AuthenticationHandler<FederationAuthenticationSchemeOptions>
{
    private readonly IPeerIdentity identity;
    private readonly IPeerStore peerStore;

    public FederationAuthenticationHandler(
        IOptionsMonitor<FederationAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IPeerIdentity identity,
        IPeerStore peerStore) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(peerStore);

        this.identity = identity;
        this.peerStore = peerStore;
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
        var request = new SignedRequest
        {
            Method = Request.Method.ToUpperInvariant(),
            Uri = Request.Path,
            Origin = sender,
            OriginServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Destination = identity.Id,
            Content = content,
            Signatures = JsonSerializer.SerializeToElement(signatures)
        };
        if (peerStore.TryGetPeer(sender, out var peer) && identity.VerifyRequest(peer, request))
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
