using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Security;

/// <summary>
/// Server-side authorization checks. Every workflow action calls these — the client is
/// never trusted ("Roles &amp; permissions"). Maps directly to the BR-* gate table.
/// </summary>
public static class Guard
{
    public static void RequireAuthenticated(ICurrentUser user)
    {
        if (!user.IsAuthenticated)
            throw new ForbiddenException("Authentication required.");
    }

    public static void RequireRole(ICurrentUser user, Role role)
    {
        RequireAuthenticated(user);
        if (user.Role != role)
            throw new ForbiddenException($"Action requires role {role}.");
    }

    /// <summary>
    /// BR-06: the user must be assigned to the court. Applied to every court-scoped action.
    /// [OPEN] decision #4: the mechanism that assigns Registry Heads to courts is unconfirmed,
    /// but the rule itself (access only assigned courts) is enforced uniformly here.
    /// </summary>
    public static void RequireAssignedCourt(ICurrentUser user, Guid courtId)
    {
        RequireAuthenticated(user);
        if (!user.IsAssignedToCourt(courtId))
            throw new ForbiddenException("User is not assigned to this court (BR-06).");
    }
}
