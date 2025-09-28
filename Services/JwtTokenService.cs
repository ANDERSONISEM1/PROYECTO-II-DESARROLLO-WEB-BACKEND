using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public sealed class JwtTokenService
{
    private readonly JwtSettings _cfg;
    private readonly SigningCredentials _creds;

    public JwtTokenService(JwtSettings cfg)
    {
        _cfg = cfg;
        _creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg.Key)),
            SecurityAlgorithms.HmacSha256);
    }

    public (string token, DateTime expiresUtc) CreateAccessToken(long userId, string username, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_cfg.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.Name, username)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = new JwtSecurityToken(
            issuer: _cfg.Issuer,
            audience: _cfg.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _creds);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, expires);
    }
}
