using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Domain.Workflow;

/// <summary>
/// The single source of truth for which copy-request state transitions are allowed
/// (WORKFLOW.md §1). Any transition not listed here is rejected.
///
/// Post-unlock (decision #3, RESOLVED): an unlocked copy is re-edited by the assigned copyist
/// and re-submitted to the reviewer for approval — i.e. Unlocked → UnderReview.
///
/// Deliberately ABSENT (PRD open decisions — do not add without confirmation):
///   • any Cancel/Void transition . no cancellation path is specified (decision #5).
/// A return/review cycle cap (decision #2) is also unspecified; Return/re-submit are allowed
/// with no cap until that decision lands.
/// </summary>
public static class CopyStateMachine
{
    // from -> set of allowed next states
    private static readonly IReadOnlyDictionary<CopyState, CopyState[]> Allowed =
        new Dictionary<CopyState, CopyState[]>
        {
            [CopyState.Created]       = [CopyState.InPreparation],
            [CopyState.InPreparation] = [CopyState.UnderReview],
            [CopyState.UnderReview]   = [CopyState.Approved, CopyState.InPreparation],
            [CopyState.Approved]      = [CopyState.Unlocked],
            [CopyState.Unlocked]      = [CopyState.UnderReview], // re-submit after re-edit (decision #3)
        };

    public static bool CanTransition(CopyState from, CopyState to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Throws <see cref="DomainException"/> if the transition is not allowed.</summary>
    public static void EnsureTransition(CopyState from, CopyState to)
    {
        if (!CanTransition(from, to))
            throw new DomainException($"Illegal copy-request transition: {from} → {to}.");
    }
}
