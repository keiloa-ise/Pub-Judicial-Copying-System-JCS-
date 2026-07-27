using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Reports;

/// <summary>
/// Composable report filter (FR-13). All parts are optional and ANDed together; they are applied
/// server-side AFTER role scoping (never trusted from the client). Date bounds are on the copy's
/// CREATION date. For role-scoped callers, CopyistId/ReviewerId are pinned to self by the service.
/// </summary>
public sealed record ReportFilter(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CopyState? Status = null,
    Guid? CourtId = null,
    Guid? RoomId = null,
    Guid? CopyistId = null,
    Guid? ReviewerId = null);

/// <summary>
/// Role-derived row constraints computed by <c>ReportService</c> from the caller's identity and
/// applied by the query layer BEFORE any client filter. A non-null id pins that column to a value;
/// <paramref name="CourtIds"/> null means "all courts" (Administrator), otherwise the row's CourtId
/// must be in the set (BR-06). This is server-trusted scope, never bound from the request.
/// </summary>
public sealed record ReportScope(
    Guid? CreatedById,
    Guid? AssignedCopyistId,
    Guid? ApprovedById,
    IReadOnlyCollection<Guid>? CourtIds);

/// <summary>Count of copies for one dimension value (court/room/copyist/reviewer), with a per-state
/// breakdown. <paramref name="Id"/> is null for the "unassigned" bucket (e.g. no reviewer yet).</summary>
public sealed record CountRow(
    Guid? Id,
    string Name,
    int Total,
    int InPreparation,
    int UnderReview,
    int Approved,
    int Unlocked);

/// <summary>Turnaround statistics (creation → approval, in hours) for one dimension value.</summary>
public sealed record TurnaroundStat(
    Guid? Id,
    string Name,
    int Count,
    double AvgHours,
    double MinHours,
    double MaxHours);

public sealed record TurnaroundReportDto(
    IReadOnlyList<TurnaroundStat> ByCourt,
    IReadOnlyList<TurnaroundStat> ByCopyist);

/// <summary>One row of the detailed copies report.</summary>
public sealed record CopyRowDto(
    Guid Id,
    string? CopyNumber,
    string CourtName,
    string RoomName,
    string CaseBaseNumber,
    string? CopyistName,
    string? ReviewerName,
    CopyState State,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ApprovedUtc,
    double? TurnaroundHours);

/// <summary>Headline numbers for the dashboard summary cards.</summary>
public sealed record ReportSummaryDto(
    int TotalCopies,
    int InPreparation,
    int UnderReview,
    int Approved,
    int Unlocked,
    int ApprovedWithTurnaround,
    double AvgTurnaroundHours,
    int AcceptedCount,
    double AvgAcceptanceHours); // FR-07: متوسط زمن القبول (creation → copyist acceptance)

public sealed record Paged<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>FR-13: one line of a judge's work log — a single decision the judge sat on (as president or
/// member), within the reservation-date range, in any state. Panel membership + delegation are read from
/// the copy's FieldValuesJson (the panel is stored there, not relationally).</summary>
public sealed record JudgeWorkLogRow(
    string JudgeName,
    string? CopyNumber,
    int? MiscNumber,
    string? DecisionNumber,
    string CourtName,
    string RoomName,
    DateOnly ReservationDate,
    CopyState State,
    string Role,               // "رئيس" | "عضو"
    bool Delegated,
    string? DelegationNumber,
    string? DelegationDate);

/// <summary>Tabular report kinds that can be exported (CSV/Excel).</summary>
public enum ReportType
{
    ByCourt,
    ByRoom,
    ByCopyist,
    ByReviewer,
    ByHead,
    ByJudge,
    JudgeWorkLog,
    Turnaround,
    Copies,
}

public enum ExportFormat
{
    Csv,
    Xlsx,
}

/// <summary>A flat, serializer-agnostic table: a title, column headers, and string-rendered rows.
/// Both the CSV and XLSX writers consume this shape so every report exports identically.</summary>
public sealed record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
