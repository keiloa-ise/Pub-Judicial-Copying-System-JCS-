using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>The caller's BR-06 scope. <see cref="RoomIds"/> is the assigned rooms for Copyists/Reviewers
/// (empty otherwise); <see cref="CourtIds"/> is the assigned courts for Heads and, for Copyists/Reviewers,
/// the courts DERIVED from their rooms (so court-granular checks/displays still resolve).</summary>
public sealed record UserScope(IReadOnlyCollection<Guid> CourtIds, IReadOnlyCollection<Guid> RoomIds);

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);

    /// <summary>The user's BR-06 scope, resolved by role: Copyist/Reviewer are room-scoped (with courts
    /// derived from those rooms); Registry Heads are court-scoped; Administrators are unrestricted (empty).</summary>
    Task<UserScope> GetAssignedScopeAsync(Guid userId, Role role, CancellationToken ct);
}
