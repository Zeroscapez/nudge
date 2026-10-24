using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nudge.Api.Models;

namespace Nudge.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

// Issues a JWT: a signed (not encrypted!) note saying "this is user 5, valid until X".
// Anyone can read it, but nobody can change it without the secret key, because the signature would break.
public class TokenService(IOptions<Infrastructure.JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly Infrastructure.JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddHours(_jwt.ExpiryHours);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
