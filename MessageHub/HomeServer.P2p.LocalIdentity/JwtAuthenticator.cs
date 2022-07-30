using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

internal class JwtAuthenticator : IDisposable
{
    private readonly RSA rsa;

    public JwtAuthenticator()
    {
        rsa = RSA.Create();
    }

    public void Dispose()
    {
        rsa.Dispose();
    }

    public string GenerateToken(string userId)
    {
        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256Signature);
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            SigningCredentials = signingCredentials,
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userId) }),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            }
        };
        return handler.CreateEncodedJwt(descriptor);
    }

    public async Task<string?> ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ValidateActor = false,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidateTokenReplay = false
        };
        var result = await tokenHandler.ValidateTokenAsync(token, validationParameters);
        return result.IsValid ? result.ClaimsIdentity.Name : null;
    }
}
