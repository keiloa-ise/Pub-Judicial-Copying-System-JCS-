using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// One correction cycle: the copyist re-submitted a decision that the reviewer had returned. It records
/// how many WORDS changed between the returned draft and the re-submitted one (additions + deletions),
/// alongside the decision's word count at re-submission. "Correctness of writing" — measured by how few
/// words needed correcting relative to the decision size — is the primary copyist-performance signal;
/// the report accumulates the per-cycle correction rate across every return of a decision. Append-only:
/// created at re-submission, never updated or deleted (mirrors <see cref="AuditEntry"/>).
/// </summary>
public sealed class CopyCorrectionStat
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CopyRequestId { get; init; }
    /// <summary>Denormalized so the report scopes/aggregates without joining the copy back.</summary>
    public Guid CourtId { get; init; }
    /// <summary>The copyist who did the correcting (assigned copyist at re-submission).</summary>
    public Guid CopyistId { get; init; }
    /// <summary>The reviewer who returned the decision (actor of the Return that opened this cycle).</summary>
    public Guid ReviewerId { get; init; }

    /// <summary>0-based order of this cycle among the decision's returns (0 = first return).</summary>
    public int CycleIndex { get; init; }

    public DateTimeOffset ReturnedUtc { get; init; }
    public DateTimeOffset ResubmittedUtc { get; init; }

    public int WordsAdded { get; init; }
    public int WordsRemoved { get; init; }
    /// <summary>Word count of the decision at re-submission — the denominator for this cycle's rate.</summary>
    public int TotalWords { get; init; }

    /// <summary>Corrected words this cycle = additions + deletions.</summary>
    public int WordsCorrected => WordsAdded + WordsRemoved;

    /// <summary>This cycle's correction rate (corrected ÷ total, 0 when the decision is empty).</summary>
    public double CorrectionRate => TotalWords <= 0 ? 0d : (double)WordsCorrected / TotalWords;

    public static CopyCorrectionStat Create(
        Guid copyRequestId, Guid courtId, Guid copyistId, Guid reviewerId, int cycleIndex,
        DateTimeOffset returnedUtc, DateTimeOffset resubmittedUtc, int wordsAdded, int wordsRemoved, int totalWords)
    {
        if (copyRequestId == Guid.Empty) throw new DomainException("Copy request is required.");
        if (copyistId == Guid.Empty) throw new DomainException("Copyist is required.");
        if (cycleIndex < 0) throw new DomainException("Cycle index cannot be negative.");
        if (wordsAdded < 0 || wordsRemoved < 0 || totalWords < 0)
            throw new DomainException("Word counts cannot be negative.");

        return new CopyCorrectionStat
        {
            CopyRequestId = copyRequestId,
            CourtId = courtId,
            CopyistId = copyistId,
            ReviewerId = reviewerId,
            CycleIndex = cycleIndex,
            ReturnedUtc = returnedUtc,
            ResubmittedUtc = resubmittedUtc,
            WordsAdded = wordsAdded,
            WordsRemoved = wordsRemoved,
            TotalWords = totalWords,
        };
    }
}
