using System.Security.Claims;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Infrastructure.Security;

namespace ResourceIQ.Jcs.Api.Auth;

/// <summary>
/// Builds <see cref="ICurrentUser"/> from the validated JWT claims on each request. The court
/// ids come from the token (issued at login from the user's assignments) and back every BR-06
/// check server-side.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        IsAuthenticated = principal?.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated || principal is null) { CourtIds = []; return; }

        Id = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? principal.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
        Name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        Role = Enum.TryParse<Role>(principal.FindFirstValue(ClaimTypes.Role), out var r) ? r : default;
        CourtIds = principal.FindAll(JwtTokenService.CourtClaim)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToHashSet();
    }

    public Guid Id { get; }
    public string Name { get; } = string.Empty;
    public Role Role { get; }
    public bool IsAuthenticated { get; }
    public IReadOnlyCollection<Guid> CourtIds { get; }

    public bool IsAssignedToCourt(Guid courtId) => CourtIds.Contains(courtId);
}
