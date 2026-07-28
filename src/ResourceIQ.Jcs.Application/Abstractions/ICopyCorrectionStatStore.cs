using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Content baseline captured when a decision was returned for correction — the reviewer-rejected
/// draft (its SectionsJson) plus who returned it and when. The re-submitted content is diffed against it
/// to count the words the copyist corrected.</summary>
public sealed record ReturnBaseline(string? SectionsJson, DateTimeOffset ReturnedUtc, Guid ReviewerId);

/// <summary>Persists copyist correction-cycle statistics (append-only) and reads the open return baseline.</summary>
public interface ICopyCorrectionStatStore
{
    /// <summary>Stage a correction stat into the current unit of work (committed by the caller).</summary>
    Task AddAsync(CopyCorrectionStat stat, CancellationToken ct);

    /// <summary>How many correction cycles are already recorded for a copy (→ the next CycleIndex).</summary>
    Task<int> CountForCopyAsync(Guid copyRequestId, CancellationToken ct);

    /// <summary>The still-open return cycle for a copy: the latest <c>Return</c> audit entry that has no
    /// <c>Submit</c> after it. Null on the very first submission (nothing was returned yet).</summary>
    Task<ReturnBaseline?> GetOpenReturnBaselineAsync(Guid copyRequestId, CancellationToken ct);
}
