namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// Every entry captures actor, timestamp, and before/after where content changes.
/// Audit history is append-only and is never deleted.
/// </summary>
public enum AuditAction
{
    Create = 1,
    Edit = 2,
    Submit = 3,
    Return = 4,
    Approve = 5,
    Unlock = 6,
    Delete = 7, // Reviewer deletes the latest copy in their court (FR-16). The copy row is removed,
                // but this audit entry (and all prior ones) is KEPT — audit is never deleted.
    Accept = 8, // Copyist accepts an assigned copy before editing it (acceptance time is recorded).
    Expedite = 9, // Registry Head escalates a non-approved copy to مستعجل (with an expedite number).
}
