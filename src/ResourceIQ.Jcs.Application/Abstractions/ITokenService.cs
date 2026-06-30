using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Issues a signed JWT carrying the user's identity, role, and court assignments.</summary>
public interface ITokenService
{
    string CreateToken(User user, IReadOnlyCollection<Guid> courtIds);
}
