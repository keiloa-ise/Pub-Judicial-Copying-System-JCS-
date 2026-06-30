namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// رقم المتفرق numbering policy, chosen per room by the Administrator (FR-06). Determines the scope
/// of the running sequence for متفرق copies created in that room:
///   • Court   — sequential at the level of the room's court.
///   • Room    — sequential at the level of the room itself.
///   • Special — sequential at a shared "special level" (A..Z) defined PER COURT, so several rooms
///               of the same court can be grouped onto one shared sequence.
/// All scopes reset yearly. Replaces the former court-type-based rule.
/// </summary>
public enum NumberingPolicy
{
    Court = 1,   // مستوى المحكمة
    Room = 2,    // مستوى الغرفة
    Special = 3, // مستوى خاص (A..Z داخل المحكمة)
}
