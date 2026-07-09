using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A room/chamber (غرفة) within a <see cref="Court"/>. A court is a set of rooms; judges are
/// assigned to rooms (not directly to the court) via <see cref="JudgeRoom"/>, and a copy request
/// targets one room. Code is unique within its court; name is required.
/// </summary>
public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourtId { get; set; }
    public Court? Court { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>رقم المتفرق numbering policy for this room (FR-06). Default = court level.</summary>
    public NumberingPolicy NumberingPolicy { get; set; } = NumberingPolicy.Court;

    /// <summary>Special level letter A..Z — set only when <see cref="NumberingPolicy"/> is Special.</summary>
    public string? NumberingLevel { get; set; }

    /// <summary>رقم النسخة (عادي) numbering scope for this room (FR-03). Default = room level.</summary>
    public CopyNumberingPolicy CopyNumberingPolicy { get; set; } = CopyNumberingPolicy.Room;

    /// <summary>The room id that scopes this room's رقم النسخة counter: the room itself when numbering
    /// at room level, or <see cref="Guid.Empty"/> (= court-wide scope) when numbering at court level.</summary>
    public Guid CopyScopeRoomId() => CopyNumberingPolicy == CopyNumberingPolicy.Room ? Id : Guid.Empty;

    public ICollection<JudgeRoom> Judges { get; set; } = new List<JudgeRoom>();

    /// <summary>The scope key identifying this room's رقم المتفرق sequence (special levels are
    /// per-court). Used by the misc-number allocator and the deletion flow.</summary>
    public string MiscScopeKey() => ScopeKey(NumberingPolicy, CourtId, Id, NumberingLevel);

    /// <summary>Builds a رقم المتفرق scope key from raw parts (shared by the allocator, read
    /// queries, and the admin counter-start screen so the format never drifts).</summary>
    public static string ScopeKey(NumberingPolicy policy, Guid courtId, Guid roomId, string? level) => policy switch
    {
        NumberingPolicy.Room => $"R:{roomId}",
        NumberingPolicy.Special => $"S:{courtId}:{level}",
        _ => $"C:{courtId}",
    };
}
