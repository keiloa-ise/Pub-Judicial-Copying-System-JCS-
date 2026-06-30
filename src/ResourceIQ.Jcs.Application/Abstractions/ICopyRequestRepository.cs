using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

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

    /// <summary>Removes the copy request (its CopyContent cascades; audit rows are untouched).</summary>
    void Remove(CopyRequest request);
}
