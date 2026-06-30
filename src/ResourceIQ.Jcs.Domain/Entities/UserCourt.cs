namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// Join row assigning a <see cref="User"/> to a <see cref="Court"/> (FR-05). Every
/// court-scoped query filters by the caller's set of these rows (BR-06) — not a UI hide.
///
/// [OPEN] PRD decision #4: Registry Head ↔ court scoping is undefined. This table can scope
/// any role; confirm how Registry Heads are scoped before depending on it for them.
/// </summary>
public class UserCourt
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid CourtId { get; set; }
    public Court? Court { get; set; }
}
