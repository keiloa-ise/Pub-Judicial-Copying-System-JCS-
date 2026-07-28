using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

public sealed class CopyCorrectionStatStore(JcsDbContext db) : ICopyCorrectionStatStore
{
    public async Task AddAsync(CopyCorrectionStat stat, CancellationToken ct) =>
        await db.CopyCorrectionStats.AddAsync(stat, ct);

    public Task<int> CountForCopyAsync(Guid copyRequestId, CancellationToken ct) =>
        db.CopyCorrectionStats.CountAsync(x => x.CopyRequestId == copyRequestId, ct);

    public async Task<ReturnBaseline?> GetOpenReturnBaselineAsync(Guid copyRequestId, CancellationToken ct)
    {
        // The most recent Return for this copy — its BeforeJson is the returned draft's SectionsJson.
        var lastReturn = await db.AuditEntries.AsNoTracking()
            .Where(a => a.CopyRequestId == copyRequestId && a.Action == AuditAction.Return)
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => new { a.BeforeJson, a.TimestampUtc, a.ActorId })
            .FirstOrDefaultAsync(ct);
        if (lastReturn is null) return null;

        // Only "open" if the copyist hasn't already re-submitted since that return (state machine makes
        // Return/Submit strictly alternate, so this closes exactly the current cycle).
        var alreadyResubmitted = await db.AuditEntries.AsNoTracking()
            .AnyAsync(a => a.CopyRequestId == copyRequestId
                          && a.Action == AuditAction.Submit
                          && a.TimestampUtc > lastReturn.TimestampUtc, ct);
        return alreadyResubmitted
            ? null
            : new ReturnBaseline(lastReturn.BeforeJson, lastReturn.TimestampUtc, lastReturn.ActorId);
    }
}
