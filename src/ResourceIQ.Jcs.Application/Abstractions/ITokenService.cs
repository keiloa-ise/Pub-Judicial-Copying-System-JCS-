using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Issues a signed JWT carrying the user's identity, role, and BR-06 scope
/// (assigned court ids + assigned room ids).</summary>
public interface ITokenService
{
    string CreateToken(User user, IReadOnlyCollection<Guid> courtIds, IReadOnlyCollection<Guid> roomIds);
}
