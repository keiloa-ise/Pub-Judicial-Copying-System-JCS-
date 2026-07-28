namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>One-off backfill of copyist correction stats from the historical audit trail, so the
/// accuracy report reflects existing data (measured from the SectionsJson snapshots that every edit
/// already recorded). Idempotent: copies that already have stats are left untouched.</summary>
public interface ICopyCorrectionBackfill
{
    /// <summary>Replays audit history and inserts missing correction-cycle rows. Returns the count added.</summary>
    Task<int> RunAsync(CancellationToken ct);
}
