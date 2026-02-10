using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CircuitBay.Application.Abstractions;
using CircuitBay.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CircuitBay.Api.Security;

public sealed class JwtTokenFactory(IOptions<JwtOptions> options) : IJwtTokenFactory
{
    public (string Token, DateTimeOffset ExpiresAt) Create(ShopperAccount user)
    {
        var settings = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(settings.Minutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expires.UtcDateTime, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
