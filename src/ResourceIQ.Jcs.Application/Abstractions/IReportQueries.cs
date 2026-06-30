using ResourceIQ.Jcs.Application.Reports;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// Read-only reporting projections (FR-13). Every method receives the server-trusted
/// <see cref="ReportScope"/> (applied before the client <see cref="ReportFilter"/>) so no query can
/// return rows outside the caller's authorization. Implemented in Infrastructure over EF Core.
/// </summary>
public interface IReportQueries
{
    Task<ReportSummaryDto> SummaryAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<CountRow>> CountByCourtAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<CountRow>> CountByRoomAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<CountRow>> CountByCopyistAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<CountRow>> CountByReviewerAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    /// <summary>FR-13: productivity per Registry Head (the copy creator).</summary>
    Task<IReadOnlyList<CountRow>> CountByHeadAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    /// <summary>FR-13: approximate productivity per judge — counts Approved copies whose panel
    /// (president/members in the content) names that judge. Names are matched as stored (free text).</summary>
    Task<IReadOnlyList<CountRow>> CountByJudgeAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<TurnaroundReportDto> TurnaroundAsync(ReportScope scope, ReportFilter filter, CancellationToken ct);
    Task<Paged<CopyRowDto>> CopiesAsync(ReportScope scope, ReportFilter filter, int page, int pageSize, CancellationToken ct);
}
