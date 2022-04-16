using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MessageHub.HomeServer;
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

    private readonly IServerIdentity serverIdentity;

    public ServerKeysController(IServerIdentity serverIdentity)
    {
        ArgumentNullException.ThrowIfNull(serverIdentity);

        this.serverIdentity = serverIdentity;
    }

    [Route("server")]
    [Route("server/{keyId}")]
    [HttpGet]
    public object GetKeys()
    {
        var expiredKeys = new Dictionary<string, OldVerifyKey>();
        foreach (var verifyKey in serverIdentity.ExpiredKeys)
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
            server_name = serverIdentity.ServerKey,
            signatures = new Dictionary<string, string>
            {
                [serverIdentity.ServerKey] = serverIdentity.Signature
            },
            valid_until_ts = serverIdentity.VerifyKeys.ExpireTimestamp,
            verify_keys = serverIdentity.VerifyKeys.Keys.ToDictionary(
                x => x.Key.ToString(),
                x => x.Value)
        };
    }
}
