namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// رقم النسخة (عادي) numbering scope for a room, chosen by the Administrator when the room is
/// created (FR-03). Independent of <see cref="NumberingPolicy"/> (which governs رقم المتفرق).
/// Default is <see cref="Room"/> — each room has its own sequential رقم النسخة.
/// </summary>
public enum CopyNumberingPolicy
{
    Court = 1, // مستوى المحكمة — all rooms in the court share one sequence
    Room = 2,  // مستوى الغرفة — each room has its own sequence (default)
}
