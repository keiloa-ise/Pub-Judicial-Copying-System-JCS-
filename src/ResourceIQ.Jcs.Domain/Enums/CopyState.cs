namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// Lifecycle states of a copy request (see WORKFLOW.md §1):
/// Created → InPreparation → UnderReview → Approved (locked) → [Admin] Unlocked.
///
/// Decision #3 (RESOLVED): an <see cref="Unlocked"/> copy is re-edited by the assigned copyist
/// and re-submitted to the reviewer (Unlocked → UnderReview), then approved again.
/// </summary>
public enum CopyState
{
    Created = 1,
    InPreparation = 2,
    UnderReview = 3,
    Approved = 4,
    Unlocked = 5,
}
