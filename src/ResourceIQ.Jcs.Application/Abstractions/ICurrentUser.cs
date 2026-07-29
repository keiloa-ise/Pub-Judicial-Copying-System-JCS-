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

    /// <summary>Courts assigned to this user — the basis for court-level BR-06 scoping (Registry Head).
    /// For Copyists/Reviewers these are DERIVED from their assigned rooms' courts.</summary>
    IReadOnlyCollection<Guid> CourtIds { get; }

    /// <summary>Rooms assigned to this user — the basis for ROOM-level BR-06 scoping (Copyist/Reviewer).
    /// Empty for Registry Heads/Administrators.</summary>
    IReadOnlyCollection<Guid> RoomIds { get; }

    bool IsAssignedToCourt(Guid courtId);
    bool IsAssignedToRoom(Guid roomId);
}
