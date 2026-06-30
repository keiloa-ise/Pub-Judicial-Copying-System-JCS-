using System.Globalization;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

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

    public Task<ReportSummaryDto> SummaryAsync(ReportFilter filter, CancellationToken ct) =>
        queries.SummaryAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByCourtAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByCourtAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByRoomAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByRoomAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByCopyistAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByCopyistAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByReviewerAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByReviewerAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByHeadAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByHeadAsync(Scope(), Safe(filter), ct);

    public Task<IReadOnlyList<CountRow>> ByJudgeAsync(ReportFilter filter, CancellationToken ct) =>
        queries.CountByJudgeAsync(Scope(), Safe(filter), ct);

    public Task<TurnaroundReportDto> TurnaroundAsync(ReportFilter filter, CancellationToken ct) =>
        queries.TurnaroundAsync(Scope(), Safe(filter), ct);

    public Task<Paged<CopyRowDto>> CopiesAsync(ReportFilter filter, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? 50 : pageSize;
        return queries.CopiesAsync(Scope(), Safe(filter), page, pageSize, ct);
    }

    // ── Scope (server-trusted, never from the request) ──
    private ReportScope Scope()
    {
        Guard.RequireAuthenticated(currentUser);
        var courts = currentUser.CourtIds; // empty => matches nothing (safe)
        return currentUser.Role switch
        {
            Role.Administrator => new ReportScope(null, null, null, null),
            Role.Reviewer => new ReportScope(null, null, currentUser.Id, courts),
            Role.Copyist => new ReportScope(null, currentUser.Id, null, courts),
            Role.RegistryHead => new ReportScope(currentUser.Id, null, null, courts),
            _ => throw new ForbiddenException("Not permitted to view reports."),
        };
    }

    /// <summary>Strips client-supplied actor filters for scoped roles (they are pinned via scope).</summary>
    private ReportFilter Safe(ReportFilter f) =>
        currentUser.Role == Role.Administrator ? f : f with { CopyistId = null, ReviewerId = null };

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
            case ReportType.Turnaround:
                return TurnaroundTable(await TurnaroundAsync(filter, ct));
            case ReportType.Copies:
                var rows = await CopiesAsync(filter, 1, MaxPageSize, ct);
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
