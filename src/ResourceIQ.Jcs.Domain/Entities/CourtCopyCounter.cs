namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// Per-scope-per-year running counter for رقم النسخة (BR-07, PRD decision #1). The scope is the
/// court (all rooms share) or a single room, per the room's <c>CopyNumberingPolicy</c> (FR-03):
/// <see cref="RoomId"/> = <see cref="Guid.Empty"/> for a court-wide sequence, or the room id for a
/// room-level one. Resets each year. Incremented atomically inside the create-request transaction.
/// Composite key (CourtId, RoomId, Year).
/// </summary>
public class CourtCopyCounter
{
    public Guid CourtId { get; set; }
    public Court? Court { get; set; }

    /// <summary>Scope of the sequence: the room id for a room-level sequence, or
    /// <see cref="Guid.Empty"/> for a court-wide one (court-level rooms share it).</summary>
    public Guid RoomId { get; set; }

    /// <summary>Calendar year the sequence belongs to.</summary>
    public int Year { get; set; }

    /// <summary>Last number issued for this scope in this year.</summary>
    public int LastNumber { get; set; }
}
