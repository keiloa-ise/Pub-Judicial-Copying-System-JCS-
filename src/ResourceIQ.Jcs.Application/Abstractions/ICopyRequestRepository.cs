using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>A copy identified for a user-facing priority-order message — its رقم النسخة (عادي) or,
/// for a متفرق, its رقم المتفرق.</summary>
public sealed record RankedCopyRef(string? CopyNumber, int? MiscNumber);

/// <summary>
/// Read/add/remove access to copy requests. Delete exists only for the Reviewer's "delete the
/// latest entry" flow (FR-16): the copy row and its content are removed, but audit history (a
/// separate entity, no cascade) is append-only and is NEVER deleted.
/// </summary>
public interface ICopyRequestRepository
{
    Task<CopyRequest?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Loads the request with its content navigation populated.</summary>
    Task<CopyRequest?> GetWithContentAsync(Guid id, CancellationToken ct);

    Task AddAsync(CopyRequest request, CancellationToken ct);

    /// <summary>BR-11: true if any متفرق copy is linked to this (original) copy — blocks its deletion.</summary>
    Task<bool> AnyLinkedMiscAsync(Guid originalCopyId, CancellationToken ct);

    /// <summary>رقم الأساس uniqueness: true if a عادي copy with this base already exists in the court.</summary>
    Task<bool> NormalCaseBaseExistsAsync(Guid courtId, string caseBaseNumber, CancellationToken ct);

    /// <summary>FR-07: true if the copyist has an unaccepted In-preparation copy that ranks BEFORE the
    /// given one — higher priority tier, or the same tier but **older** (created earlier). Acceptance
    /// must follow: موقوف > مستعجل > عادي, and within a tier oldest-first (BR-10/BR-13).</summary>
    Task<bool> AnyUnacceptedRankedBeforeAsync(
        Guid copyistId, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct);

    /// <summary>FR-10/BR-10: the copies still UNDER REVIEW in the reviewer's <paramref name="roomIds"/>
    /// that rank BEFORE the given one — higher priority tier, or the same tier but **older** — ordered by
    /// that same priority (موقوف > مستعجل > عادي, then oldest-first) so the FIRST is the next to approve.
    /// Empty when the given copy is next in line. Used to both gate approval and name the blocking copies.</summary>
    Task<IReadOnlyList<RankedCopyRef>> ListUnderReviewRankedBeforeAsync(
        IReadOnlyCollection<Guid> roomIds, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct);

    /// <summary>FR-15 print ordering: true if any NOT-YET-PRINTED copy in the given courts, in the same
    /// print queue (<paramref name="isApproved"/> = approved vs draft), ranks BEFORE the given one —
    /// higher priority tier, or the same tier but older. Printing follows موقوف > مستعجل > عادي then
    /// oldest-first, with the approved and non-approved queues ordered independently.</summary>
    Task<bool> AnyUnprintedRankedBeforeAsync(
        IReadOnlyCollection<Guid> courtIds, bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct);

    /// <summary>Removes the copy request (its CopyContent cascades; audit rows are untouched).</summary>
    void Remove(CopyRequest request);
}
