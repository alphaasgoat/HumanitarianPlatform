using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Humanitarian.Infrastructure.Security;

public class JwtSettings
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public class JwtTokenService(IOptions<JwtSettings> options, TimeProvider time) : ITokenService
{
    private readonly JwtSettings _s = options.Value;

    public TokenResult CreateToken(User user)
    {
        var expires = time.GetUtcNow().UtcDateTime.AddMinutes(_s.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("name", user.FullName),
            new("role", user.Role.ToString()),
            new("jti", Guid.NewGuid().ToString())
        };
        if (user.OrganisationId is { } org) claims.Add(new Claim("org", org.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Key));
        var token = new JwtSecurityToken(_s.Issuer, _s.Audience, claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
