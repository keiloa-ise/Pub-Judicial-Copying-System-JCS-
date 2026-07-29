using System.Globalization;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.Reports;

/// <summary>
/// Reporting (FR-13), read-only. Every method derives a server-trusted <see cref="ReportScope"/>
/// from the caller's role/identity BEFORE applying the client filter (BR-06), so a user can only
/// ever see their own slice. Scoped roles (Reviewer/Copyist/RegistryHead) have the relevant
/// dimension pinned to self; the client's CopyistId/ReviewerId are ignored for them. Export reuses
/// the very same scope+query path, so downloaded files match the on-screen report exactly.
/// </summary>
public sealed class ReportService(ICurrentUser currentUser, IReportQueries queries)
{
    private const int MaxPageSize = 200;
    /// <summary>Upper bound on rows pulled for a file export (detail reports), so a download stays
    /// bounded even within the allowed date range. Larger than a screen page, far below "the whole table".</summary>
    private const int MaxExportRows = 20_000;

    public Task<ReportSummaryDto> SummaryAsync(ReportFilter filter, CancellationToken ct) =>
        queries.SummaryAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByCourtAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByCourtAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByRoomAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByRoomAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByCopyistAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByCopyistAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByReviewerAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByReviewerAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByHeadAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByHeadAsync(Scope(), Require(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByJudgeAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByJudgeAsync(Scope(), Require(filter), ct);

    // Row-level detail reports additionally cap the range span so they can never scan a whole year+ at once.
    public Task<Paged<JudgeWorkLogRow>> JudgeWorkLogAsync(ReportFilter filter, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? 50 : pageSize;
        return queries.JudgeWorkLogAsync(Scope(), Require(filter, MaxDetailRangeDays), page, pageSize, ct);
    }

    public Task<IReadOnlyList<CopyistAccuracyRow>> CopyistAccuracyAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CopyistAccuracyAsync(Scope(), Require(filter), ct);

    public Task<TurnaroundReportDto> TurnaroundAsync(ReportFilter filter, CancellationToken ct) =>
        queries.TurnaroundAsync(Scope(), Require(filter), ct);

    public Task<Paged<CopyRowDto>> CopiesAsync(ReportFilter filter, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? 50 : pageSize;
        return queries.CopiesAsync(Scope(), Require(filter, MaxDetailRangeDays), page, pageSize, ct);
    }

    // ── Scope (server-trusted, never from the request) ──
    private ReportScope Scope()
    {
        Guard.RequireAuthenticated(currentUser);
        var courts = currentUser.CourtIds; // empty => matches nothing (safe)
        var rooms = currentUser.RoomIds;   // Copyist/Reviewer room scope (BR-06)
        return currentUser.Role switch
        {
            Role.Administrator => new ReportScope(null, null, null, null),
            // Copyist/Reviewer are ROOM-scoped: RoomIds tightens to their rooms where the row has one;
            // the derived CourtIds is kept as a safe fallback for reports without a RoomId column.
            Role.Reviewer => new ReportScope(null, null, currentUser.Id, courts, rooms),
            Role.Copyist => new ReportScope(null, currentUser.Id, null, courts, rooms),
            Role.RegistryHead => new ReportScope(currentUser.Id, null, null, courts),
            _ => throw new ForbiddenException("Not permitted to view reports."),
        };
    }

    /// <summary>Validates the date range and strips client-supplied actor filters for scoped roles
    /// (they are pinned via scope). Every report + export method passes through here, so this is the
    /// single choke point for both concerns — the client is never trusted (BR-06 posture).</summary>
    private ReportFilter Safe(ReportFilter f)
    {
        if (f.FromDate is { } from && f.ToDate is { } to && from > to)
            throw new DomainException("يجب أن يكون تاريخ البداية أقل من تاريخ النهاية أو يساويه.");
        return currentUser.Role == Role.Administrator ? f : f with { CopyistId = null, ReviewerId = null };
    }

    /// <summary>Max span (days) for the row-level detail reports (تفاصيل النسخ، سجل القضاة) — a bounded
    /// window so they can never scan an unbounded date range at 500k+ rows.</summary>
    private const int MaxDetailRangeDays = 366;

    /// <summary>Reports must not run without appropriate filters: a bounded date range (من/إلى) is
    /// mandatory for EVERY report, enforced server-side (the client guard is only a convenience). The
    /// heavy detail reports also cap the span. Runs through <see cref="Safe"/> first (order + actor strip).</summary>
    private ReportFilter Require(ReportFilter f, int? maxSpanDays = null)
    {
        var safe = Safe(f);
        if (safe.FromDate is not { } from || safe.ToDate is not { } to)
            throw new DomainException("يجب تحديد المدى التاريخي (من/إلى) قبل تشغيل التقرير.");
        if (maxSpanDays is { } max && to.DayNumber - from.DayNumber > max)
            throw new DomainException($"المدى التاريخي كبير جدًا؛ الحد الأقصى {max} يومًا. الرجاء تضييق الفترة.");
        return safe;
    }

    // ── Export: build a flat table for a report type (all matching rows; no paging) ──
    public async Task<ReportTable> BuildTableAsync(ReportType type, ReportFilter filter, CancellationToken ct)
    {
        switch (type)
        {
            case ReportType.ByCourt:
                return CountTable("تقرير النسخ حسب المحكمة", "المحكمة", await ByCourtAsync(filter, ct));
            case ReportType.ByRoom:
                return CountTable("تقرير النسخ حسب الغرفة", "الغرفة", await ByRoomAsync(filter, ct));
            case ReportType.ByCopyist:
                return CountTable("تقرير النسخ حسب الناسخ", "الناسخ", await ByCopyistAsync(filter, ct));
            case ReportType.ByReviewer:
                return CountTable("تقرير النسخ حسب المدقق", "المدقق", await ByReviewerAsync(filter, ct));
            case ReportType.ByHead:
                return CountTable("تقرير النسخ حسب رئيس الديوان", "رئيس الديوان", await ByHeadAsync(filter, ct));
            case ReportType.ByJudge:
                return CountTable("تقرير النسخ حسب القاضي (تقريبي)", "القاضي", await ByJudgeAsync(filter, ct));
            case ReportType.JudgeWorkLog:
                // Export the full (bounded) range, not just one screen page — cap for safety.
                var log = await queries.JudgeWorkLogAsync(Scope(), Require(filter, MaxDetailRangeDays), 1, MaxExportRows, ct);
                return JudgeWorkLogTable(log.Items);
            case ReportType.CopyistAccuracy:
                return CopyistAccuracyTable(await CopyistAccuracyAsync(filter, ct));
            case ReportType.Turnaround:
                return TurnaroundTable(await TurnaroundAsync(filter, ct));
            case ReportType.Copies:
                var rows = await queries.CopiesAsync(Scope(), Require(filter, MaxDetailRangeDays), 1, MaxExportRows, ct);
                return CopiesTable(rows.Items);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown report type.");
        }
    }

    private static ReportTable CountTable(string title, string dimension, IReadOnlyList<CountRow> rows)
    {
        var headers = new[] { dimension, "الإجمالي", "قيد التحضير", "قيد المراجعة", "معتمد", "مفتوح" };
        var data = rows.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Name,
            r.Total.ToString(CultureInfo.InvariantCulture),
            r.InPreparation.ToString(CultureInfo.InvariantCulture),
            r.UnderReview.ToString(CultureInfo.InvariantCulture),
            r.Approved.ToString(CultureInfo.InvariantCulture),
            r.Unlocked.ToString(CultureInfo.InvariantCulture),
        }).ToList();
        return new ReportTable(title, headers, data);
    }

    private static ReportTable TurnaroundTable(TurnaroundReportDto dto)
    {
        var headers = new[] { "النطاق", "الاسم", "العدد", "المتوسط (ساعات)", "الأدنى (ساعات)", "الأقصى (ساعات)" };
        var data = new List<IReadOnlyList<string>>();
        foreach (var s in dto.ByCourt) data.Add(Row("محكمة", s));
        foreach (var s in dto.ByCopyist) data.Add(Row("ناسخ", s));
        return new ReportTable("تقرير مدة الإنجاز (من الإنشاء إلى الاعتماد)", headers, data);

        static IReadOnlyList<string> Row(string kind, TurnaroundStat s) => new[]
        {
            kind, s.Name,
            s.Count.ToString(CultureInfo.InvariantCulture),
            s.AvgHours.ToString("0.0", CultureInfo.InvariantCulture),
            s.MinHours.ToString("0.0", CultureInfo.InvariantCulture),
            s.MaxHours.ToString("0.0", CultureInfo.InvariantCulture),
        };
    }

    private static ReportTable CopiesTable(IReadOnlyList<CopyRowDto> rows)
    {
        var headers = new[]
        {
            "رقم النسخة", "المحكمة", "الغرفة", "رقم الأساس", "الناسخ", "المدقق",
            "الحالة", "تاريخ الإنشاء", "تاريخ الاعتماد", "مدة الإنجاز (ساعات)",
        };
        var data = rows.Select(r => (IReadOnlyList<string>)new[]
        {
            r.CopyNumber ?? "",
            r.CourtName, r.RoomName, r.CaseBaseNumber,
            r.CopyistName ?? "", r.ReviewerName ?? "",
            StateLabel(r.State),
            r.CreatedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            r.ApprovedUtc?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
            r.TurnaroundHours?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
        }).ToList();
        return new ReportTable("تقرير النسخ", headers, data);
    }

    private static ReportTable CopyistAccuracyTable(IReadOnlyList<CopyistAccuracyRow> rows)
    {
        var headers = new[]
        {
            "المحرِّر", "نسبة التصحيح", "قرارات مصحّحة", "دورات الإرجاع", "كلمات مصحّحة", "إجمالي الكلمات",
        };
        var data = rows.Select(r => (IReadOnlyList<string>)new[]
        {
            r.CopyistName,
            (r.AvgCorrectionRate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%",
            r.DecisionsCorrected.ToString(CultureInfo.InvariantCulture),
            r.ReturnCycles.ToString(CultureInfo.InvariantCulture),
            r.TotalWordsCorrected.ToString(CultureInfo.InvariantCulture),
            r.TotalWords.ToString(CultureInfo.InvariantCulture),
        }).ToList();
        return new ReportTable("تقرير دقّة عمل المحرِّر (صحّة الكتابة)", headers, data);
    }

    private static ReportTable JudgeWorkLogTable(IReadOnlyList<JudgeWorkLogRow> rows)
    {
        var headers = new[]
        {
            "القاضي", "الدور", "رقم النسخة/المتفرق", "رقم القرار", "المحكمة", "الغرفة",
            "تاريخ الحجز", "الحالة", "منتدب", "رقم الندب", "تاريخ الندب",
        };
        var data = rows.Select(r => (IReadOnlyList<string>)new[]
        {
            r.JudgeName, r.Role,
            r.CopyNumber ?? (r.MiscNumber is { } m ? $"متفرق {m}" : ""),
            r.DecisionNumber ?? "",
            r.CourtName, r.RoomName,
            r.ReservationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StateLabel(r.State),
            r.Delegated ? "نعم" : "لا",
            r.DelegationNumber ?? "", r.DelegationDate ?? "",
        }).ToList();
        return new ReportTable("سجل أعمال القضاة", headers, data);
    }

    public static string StateLabel(CopyState s) => s switch
    {
        CopyState.Created => "أُنشئ",
        CopyState.InPreparation => "قيد التحضير",
        CopyState.UnderReview => "قيد المراجعة",
        CopyState.Approved => "معتمد",
        CopyState.Unlocked => "مفتوح",
        _ => s.ToString(),
    };
}
