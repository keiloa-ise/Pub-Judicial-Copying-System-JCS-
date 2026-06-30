using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Security;

/// <summary>
/// Issues signed JWTs. Claims carry the user id, role, and assigned court ids — the API
/// rebuilds <see cref="ICurrentUser"/> from these and re-checks every action server-side.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public const string CourtClaim = "court";

    private readonly JwtOptions _o = options.Value;

    public string CreateToken(User user, IReadOnlyCollection<Guid> courtIds)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(courtIds.Select(id => new Claim(CourtClaim, id.ToString())));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _o.Issuer,
            audience: _o.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_o.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
