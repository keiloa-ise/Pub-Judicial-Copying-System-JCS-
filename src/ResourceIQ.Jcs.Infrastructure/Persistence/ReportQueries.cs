using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>
/// EF Core reporting projections (FR-13). All aggregation is set-based in SQL Server: counts via
/// GroupBy, turnaround via DATEDIFF(second, created, approved) through EF.Functions.DateDiffSecond.
/// Scope is applied before the filter so no query can leak rows outside the caller's authorization.
/// </summary>
public sealed class ReportQueries(JcsDbContext db) : IReportQueries
{
    private const string Unassigned = "—";

    private sealed record GroupCount(Guid? Key, int Total, int InPreparation, int UnderReview, int Approved, int Unlocked);

    /// <summary>Scoped + filtered base set. Scope (server-trusted) first, then the client filter.</summary>
    private IQueryable<CopyRequest> Base(ReportScope scope, ReportFilter f)
    {
        var q = db.CopyRequests.AsNoTracking();

        if (scope.CreatedById is { } cb) q = q.Where(x => x.CreatedById == cb);
        if (scope.AssignedCopyistId is { } sc) q = q.Where(x => x.AssignedCopyistId == sc);
        if (scope.ApprovedById is { } sr) q = q.Where(x => x.ApprovedById == sr);
        if (scope.CourtIds is not null)
        {
            var ids = scope.CourtIds.ToArray();
            q = q.Where(x => ids.Contains(x.CourtId));
        }

        if (f.FromDate is { } fd)
        {
            var from = new DateTimeOffset(fd.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            q = q.Where(x => x.CreatedUtc >= from);
        }
        if (f.ToDate is { } td)
        {
            var toExcl = new DateTimeOffset(td.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            q = q.Where(x => x.CreatedUtc < toExcl);
        }
        if (f.Status is { } st) q = q.Where(x => x.State == st);
        if (f.CourtId is { } fc) q = q.Where(x => x.CourtId == fc);
        if (f.RoomId is { } fr) q = q.Where(x => x.RoomId == fr);
        if (f.CopyistId is { } fcp) q = q.Where(x => x.AssignedCopyistId == fcp);
        if (f.ReviewerId is { } frv) q = q.Where(x => x.ApprovedById == frv);

        return q;
    }

    private static Task<List<GroupCount>> GroupAsync(
        IQueryable<CopyRequest> q, Expression<Func<CopyRequest, Guid?>> key, CancellationToken ct) =>
        q.GroupBy(key).Select(g => new GroupCount(
            g.Key,
            g.Count(),
            g.Count(x => x.State == CopyState.InPreparation),
            g.Count(x => x.State == CopyState.UnderReview),
            g.Count(x => x.State == CopyState.Approved),
            g.Count(x => x.State == CopyState.Unlocked))).ToListAsync(ct);

    private static IReadOnlyList<CountRow> ToRows(List<GroupCount> groups, IReadOnlyDictionary<Guid, string> names) =>
        groups
            .Select(g => new CountRow(
                g.Key,
                g.Key is { } k ? (names.TryGetValue(k, out var n) ? n : Unassigned) : Unassigned,
                g.Total, g.InPreparation, g.UnderReview, g.Approved, g.Unlocked))
            .OrderByDescending(r => r.Total).ThenBy(r => r.Name)
            .ToList();

    private async Task<IReadOnlyDictionary<Guid, string>> CourtNamesAsync(IEnumerable<GroupCount> g, CancellationToken ct)
    {
        var ids = g.Where(x => x.Key != null).Select(x => x.Key!.Value).Distinct().ToArray();
        return await db.Courts.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RoomNamesAsync(IEnumerable<GroupCount> g, CancellationToken ct)
    {
        var ids = g.Where(x => x.Key != null).Select(x => x.Key!.Value).Distinct().ToArray();
        return await db.Rooms.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> keys, CancellationToken ct)
    {
        var ids = keys.Where(x => x != null).Select(x => x!.Value).Distinct().ToArray();
        return await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public async Task<IReadOnlyList<CountRow>> CountByCourtAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var g = await GroupAsync(Base(scope, filter), x => x.CourtId, ct);
        return ToRows(g, await CourtNamesAsync(g, ct));
    }

    public async Task<IReadOnlyList<CountRow>> CountByRoomAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var g = await GroupAsync(Base(scope, filter), x => x.RoomId, ct);
        return ToRows(g, await RoomNamesAsync(g, ct));
    }

    public async Task<IReadOnlyList<CountRow>> CountByCopyistAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var g = await GroupAsync(Base(scope, filter), x => x.AssignedCopyistId, ct);
        return ToRows(g, await UserNamesAsync(g.Select(x => x.Key), ct));
    }

    public async Task<IReadOnlyList<CountRow>> CountByReviewerAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var g = await GroupAsync(Base(scope, filter), x => x.ApprovedById, ct);
        return ToRows(g, await UserNamesAsync(g.Select(x => x.Key), ct));
    }

    public async Task<IReadOnlyList<CountRow>> CountByHeadAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var g = await GroupAsync(Base(scope, filter), x => (Guid?)x.CreatedById, ct);
        return ToRows(g, await UserNamesAsync(g.Select(x => x.Key), ct));
    }

    public async Task<IReadOnlyList<CountRow>> CountByJudgeAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        // Approximate: the deciding panel is stored as free-text names in the content (president/members)
        // of Approved copies. Aggregate the count of Approved copies per judge name.
        var rows = await (from cr in Base(scope, filter).Where(x => x.State == CopyState.Approved)
                          join c in db.CopyContents.AsNoTracking() on cr.Id equals c.CopyRequestId
                          select c.FieldValuesJson).ToListAsync(ct);
        var counts = new Dictionary<string, int>();
        foreach (var json in rows)
            foreach (var name in ExtractPanel(json))
                counts[name] = counts.GetValueOrDefault(name) + 1;

        return counts
            .Select(kv => new CountRow(null, kv.Key, kv.Value, 0, 0, kv.Value, 0)) // Approved-only
            .OrderByDescending(r => r.Total).ThenBy(r => r.Name)
            .ToList();
    }

    private static IEnumerable<string> ExtractPanel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(json); } catch { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) yield break;
            var seen = new HashSet<string>();
            if (doc.RootElement.TryGetProperty("president", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var n = p.GetString()?.Trim();
                if (!string.IsNullOrEmpty(n) && seen.Add(n)) yield return n;
            }
            if (doc.RootElement.TryGetProperty("members", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var e in m.EnumerateArray())
                {
                    var n = (e.ValueKind == System.Text.Json.JsonValueKind.String ? e.GetString() : null)?.Trim();
                    if (!string.IsNullOrEmpty(n) && seen.Add(n)) yield return n;
                }
        }
    }

    public async Task<ReportSummaryDto> SummaryAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var q = Base(scope, filter);
        var counts = await q.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            InPreparation = g.Count(x => x.State == CopyState.InPreparation),
            UnderReview = g.Count(x => x.State == CopyState.UnderReview),
            Approved = g.Count(x => x.State == CopyState.Approved),
            Unlocked = g.Count(x => x.State == CopyState.Unlocked),
        }).FirstOrDefaultAsync(ct);

        var approvedQ = q.Where(x => x.ApprovedUtc != null);
        var taCount = await approvedQ.CountAsync(ct);
        double avgHours = 0;
        if (taCount > 0)
        {
            var avgSec = await approvedQ.AverageAsync(
                x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value), ct);
            avgHours = Math.Round(avgSec / 3600.0, 2);
        }

        // FR-07: time-to-acceptance (creation → copyist acceptance).
        var acceptedQ = q.Where(x => x.AcceptedUtc != null);
        var accCount = await acceptedQ.CountAsync(ct);
        double avgAccHours = 0;
        if (accCount > 0)
        {
            var avgSec = await acceptedQ.AverageAsync(
                x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.AcceptedUtc!.Value), ct);
            avgAccHours = Math.Round(avgSec / 3600.0, 2);
        }

        return counts is null
            ? new ReportSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0)
            : new ReportSummaryDto(counts.Total, counts.InPreparation, counts.UnderReview,
                counts.Approved, counts.Unlocked, taCount, avgHours, accCount, avgAccHours);
    }

    public async Task<TurnaroundReportDto> TurnaroundAsync(ReportScope scope, ReportFilter filter, CancellationToken ct)
    {
        var q = Base(scope, filter).Where(x => x.ApprovedUtc != null);

        var byCourtRaw = await q.GroupBy(x => x.CourtId).Select(g => new
        {
            Key = g.Key,
            Count = g.Count(),
            AvgSec = g.Average(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
            MinSec = g.Min(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
            MaxSec = g.Max(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
        }).ToListAsync(ct);

        var byCopyistRaw = await q.Where(x => x.AssignedCopyistId != null).GroupBy(x => x.AssignedCopyistId!.Value).Select(g => new
        {
            Key = g.Key,
            Count = g.Count(),
            AvgSec = g.Average(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
            MinSec = g.Min(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
            MaxSec = g.Max(x => (double)EF.Functions.DateDiffSecond(x.CreatedUtc, x.ApprovedUtc!.Value)),
        }).ToListAsync(ct);

        var courtNames = await db.Courts
            .Where(c => byCourtRaw.Select(r => r.Key).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var copyistNames = await db.Users
            .Where(u => byCopyistRaw.Select(r => r.Key).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        static TurnaroundStat Stat(Guid key, string name, int count, double avgSec, double minSec, double maxSec) =>
            new(key, name, count, Math.Round(avgSec / 3600.0, 2), Math.Round(minSec / 3600.0, 2), Math.Round(maxSec / 3600.0, 2));

        var byCourt = byCourtRaw
            .Select(r => Stat(r.Key, courtNames.GetValueOrDefault(r.Key, Unassigned), r.Count, r.AvgSec, r.MinSec, r.MaxSec))
            .OrderBy(s => s.Name).ToList();
        var byCopyist = byCopyistRaw
            .Select(r => Stat(r.Key, copyistNames.GetValueOrDefault(r.Key, Unassigned), r.Count, r.AvgSec, r.MinSec, r.MaxSec))
            .OrderBy(s => s.Name).ToList();

        return new TurnaroundReportDto(byCourt, byCopyist);
    }

    public async Task<Paged<CopyRowDto>> CopiesAsync(
        ReportScope scope, ReportFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var q = Base(scope, filter);
        var total = await q.CountAsync(ct);

        var pageQ = q.OrderByDescending(x => x.CreatedUtc).Skip((page - 1) * pageSize).Take(pageSize);

        var raw = await (
            from x in pageQ
            join c in db.Courts on x.CourtId equals c.Id
            join rm in db.Rooms on x.RoomId equals rm.Id
            join cpU in db.Users on x.AssignedCopyistId equals cpU.Id into cpj
            from cp in cpj.DefaultIfEmpty()
            join rvU in db.Users on x.ApprovedById equals rvU.Id into rvj
            from rv in rvj.DefaultIfEmpty()
            select new
            {
                x.Id, x.CopyNumber, Court = c.Name, Room = rm.Name, x.CaseBaseNumber,
                Copyist = (string?)(cp != null ? cp.DisplayName : null),
                Reviewer = (string?)(rv != null ? rv.DisplayName : null),
                x.State, x.CreatedUtc, x.ApprovedUtc,
            }).ToListAsync(ct);

        var items = raw
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => new CopyRowDto(
                r.Id, r.CopyNumber, r.Court, r.Room, r.CaseBaseNumber, r.Copyist, r.Reviewer,
                r.State, r.CreatedUtc, r.ApprovedUtc,
                r.ApprovedUtc != null ? Math.Round((r.ApprovedUtc.Value - r.CreatedUtc).TotalHours, 2) : null))
            .ToList();

        return new Paged<CopyRowDto>(items, total, page, pageSize);
    }
}
