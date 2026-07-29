namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// Join row assigning a <see cref="User"/> to a <see cref="Room"/> — the ROOM-level scope for
/// Copyists and Reviewers (a copyist/reviewer may serve one or more rooms, across one or more
/// courts). Their court scope (for the checks/queries that remain court-granular) is DERIVED from
/// the courts of these rooms. Registry Heads stay court-scoped via <see cref="UserCourt"/>.
/// Every room-scoped query filters by the caller's set of these rows (BR-06) — not a UI hide.
/// </summary>
public class UserRoom
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
}
