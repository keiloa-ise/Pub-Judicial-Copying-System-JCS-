using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>
/// Reconstructs correction-cycle stats from history. For each copy it replays its Edit/Return/Submit
/// audit entries in order, tracking the current SectionsJson (last Edit's AfterJson); a Return opens a
/// cycle whose baseline is the returned draft (its own BeforeJson if present, else the reconstructed
/// current content), and the next Submit closes it — diffing baseline vs current to count corrected words.
/// </summary>
public sealed class CopyCorrectionBackfillService(JcsDbContext db) : ICopyCorrectionBackfill
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var alreadyDone = (await db.CopyCorrectionStats.AsNoTracking()
            .Select(s => s.CopyRequestId).Distinct().ToListAsync(ct)).ToHashSet();

        var copies = await db.CopyRequests.AsNoTracking()
            .Select(c => new { c.Id, c.CourtId, c.AssignedCopyistId })
            .ToListAsync(ct);
        var copyInfo = copies.ToDictionary(c => c.Id);

        var entries = await db.AuditEntries.AsNoTracking()
            .Where(a => a.Action == AuditAction.Edit || a.Action == AuditAction.Return || a.Action == AuditAction.Submit)
            .OrderBy(a => a.TimestampUtc)
            .Select(a => new { a.CopyRequestId, a.Action, a.TimestampUtc, a.ActorId, a.BeforeJson, a.AfterJson })
            .ToListAsync(ct);

        var created = new List<CopyCorrectionStat>();
        foreach (var g in entries.GroupBy(e => e.CopyRequestId)) // group preserves the time order within each copy
        {
            if (alreadyDone.Contains(g.Key)) continue;
            if (!copyInfo.TryGetValue(g.Key, out var info) || info.AssignedCopyistId is not { } copyistId) continue;

            string? current = null, baseline = null;
            DateTimeOffset returnedUtc = default;
            Guid reviewerId = default;
            bool pending = false;
            int cycle = 0;

            foreach (var e in g)
            {
                switch (e.Action)
                {
                    case AuditAction.Edit:
                        if (e.AfterJson is not null) current = e.AfterJson;
                        break;
                    case AuditAction.Return:
                        baseline = e.BeforeJson ?? current;
                        returnedUtc = e.TimestampUtc;
                        reviewerId = e.ActorId;
                        pending = true;
                        break;
                    case AuditAction.Submit:
                        if (!pending) break;
                        var before = CorrectionMetrics.ExtractPlainText(baseline);
                        var after = CorrectionMetrics.ExtractPlainText(current);
                        var delta = CorrectionMetrics.Diff(before, after);
                        created.Add(CopyCorrectionStat.Create(
                            g.Key, info.CourtId, copyistId, reviewerId, cycle++,
                            returnedUtc, e.TimestampUtc, delta.Added, delta.Removed, CorrectionMetrics.WordCount(after)));
                        pending = false;
                        break;
                }
            }
        }

        if (created.Count > 0)
        {
            await db.CopyCorrectionStats.AddRangeAsync(created, ct);
            await db.SaveChangesAsync(ct);
        }
        return created.Count;
    }
}
