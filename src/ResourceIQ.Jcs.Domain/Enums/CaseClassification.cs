namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// Classifications the Registry Head chooses when creating a copy request. Names map to the
/// Arabic UI labels. Stored on <see cref="Entities.CopyRequest"/>; required at creation.
/// </summary>

/// <summary>التصنيف: عادي / متفرق ("مستعجل" moved to <see cref="CaseUrgency"/>).</summary>
public enum CaseCategory
{
    Normal = 1,        // عادي
    Miscellaneous = 3, // متفرق
}

/// <summary>
/// الحالة: عادي / موقوف / مستعجل. Drives work-queue execution priority:
/// موقوف (highest) > مستعجل > عادي (default, lowest). "عادي" is the default at creation.
/// </summary>
public enum CaseUrgency
{
    Suspended = 1, // موقوف — highest execution priority
    Expedited = 2, // مستعجل — below موقوف; requires an expedite-request number
    Normal = 3,    // عادي — default; lowest priority
}
