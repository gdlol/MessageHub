using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.Federation;

[Route("_matrix/key/{version}")]
[AllowAnonymous]
public class ServerKeysController : ControllerBase
{
    public class OldVerifyKey
    {
        [Required]
        [JsonPropertyName("expired_ts")]
        public long ExpiredTimestamp { get; set; }

        [Required]
        [JsonPropertyName("key")]
        public string Key { get; set; } = default!;
    }

    private readonly IPeerIdentity peerIdentity;

    public ServerKeysController(IPeerIdentity peerIdentity)
    {
        ArgumentNullException.ThrowIfNull(peerIdentity);

        this.peerIdentity = peerIdentity;
    }

    [Route("server")]
    [Route("server/{keyId}")]
    [HttpGet]
    public object GetKeys()
    {
        var expiredKeys = new Dictionary<string, OldVerifyKey>();
        foreach (var verifyKey in peerIdentity.ExpiredKeys)
        {
            foreach (var (identifier, key) in verifyKey.Keys)
            {
                expiredKeys[identifier.ToString()] = new OldVerifyKey
                {
                    ExpiredTimestamp = verifyKey.ExpireTimestamp,
                    Key = key
                };
            }
        }
        return new
        {
            old_verify_keys = expiredKeys,
            server_name = peerIdentity.Id,
            signatures = new Signatures
            {
                [peerIdentity.Id] = new ServerSignatures
                {
                    [new KeyIdentifier(peerIdentity.SignatureAlgorithm, peerIdentity.Id).ToString()]
                        = peerIdentity.Signature
                }
            },
            valid_until_ts = peerIdentity.VerifyKeys.ExpireTimestamp,
            verify_keys = peerIdentity.VerifyKeys.Keys.ToDictionary(
                x => x.Key.ToString(),
                x => x.Value)
        };
    }
}
