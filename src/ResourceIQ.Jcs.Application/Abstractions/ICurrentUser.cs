using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// The authenticated caller, resolved per-request from the JWT in the API layer.
/// Authorization is always re-checked server-side against this (never trusted from client).
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    string Name { get; }
    Role Role { get; }
    bool IsAuthenticated { get; }

    /// <summary>Courts assigned to this user — the basis for BR-06 scoping.</summary>
    IReadOnlyCollection<Guid> CourtIds { get; }

    bool IsAssignedToCourt(Guid courtId);
}
