namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// Per-court-per-year running counter for copy numbers (BR-07, uniqueness scope = per-court,
/// PRD decision #1). The sequence resets each year per court; the formatted copy number embeds
/// the court code and year (e.g. C-001/2026/0001). Incremented atomically inside the
/// create-request transaction. Composite key (CourtId, Year).
/// </summary>
public class CourtCopyCounter
{
    public Guid CourtId { get; set; }
    public Court? Court { get; set; }

    /// <summary>Calendar year the sequence belongs to.</summary>
    public int Year { get; set; }

    /// <summary>Last number issued for this court in this year.</summary>
    public int LastNumber { get; set; }
}
