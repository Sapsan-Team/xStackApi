using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public AccessToken CreateToken(AuthUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(options.SigningKey),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("phone_number", user.PhoneNumber)
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
