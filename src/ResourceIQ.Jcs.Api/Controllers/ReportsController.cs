using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Reports;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// Reporting (FR-13), read-only. [Authorize] requires a token; data is scoped to the caller's role
/// and assigned courts inside <see cref="ReportService"/> (BR-06) — never trusted from the client.
/// Read endpoints are typed (one per report shape); export is one parameterized endpoint that reuses
/// the same scoped query path, so files match the on-screen report exactly.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(ReportService reports, IReportExporter exporter) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.SummaryAsync(filter, ct));

    [HttpGet("by-court")]
    public async Task<IActionResult> ByCourt([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByCourtAsync(filter, ct));

    [HttpGet("by-room")]
    public async Task<IActionResult> ByRoom([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByRoomAsync(filter, ct));

    [HttpGet("by-copyist")]
    public async Task<IActionResult> ByCopyist([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByCopyistAsync(filter, ct));

    [HttpGet("by-reviewer")]
    public async Task<IActionResult> ByReviewer([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByReviewerAsync(filter, ct));

    [HttpGet("by-head")]
    public async Task<IActionResult> ByHead([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByHeadAsync(filter, ct));

    [HttpGet("by-judge")]
    public async Task<IActionResult> ByJudge([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.ByJudgeAsync(filter, ct));

    [HttpGet("turnaround")]
    public async Task<IActionResult> Turnaround([FromQuery] ReportFilter filter, CancellationToken ct) =>
        Ok(await reports.TurnaroundAsync(filter, ct));

    [HttpGet("copies")]
    public async Task<IActionResult> Copies(
        [FromQuery] ReportFilter filter, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await reports.CopiesAsync(filter, page, pageSize, ct));

    /// <summary>Export any tabular report as CSV (UTF-8+BOM) or XLSX, honoring the same scope+filters.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string type, [FromQuery] string format, [FromQuery] ReportFilter filter, CancellationToken ct)
    {
        if (!TryParseType(type, out var reportType))
            return BadRequest(new { error = $"Unknown report type '{type}'.", status = 400 });

        var fmt = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
            ? ExportFormat.Xlsx : ExportFormat.Csv;

        var table = await reports.BuildTableAsync(reportType, filter, ct);
        var result = exporter.Export(table, fmt);
        // FileContentResult emits an RFC 6266 Content-Disposition (filename + filename*) so the
        // Arabic title downloads correctly across browsers.
        return File(result.Content, result.ContentType, $"{table.Title}.{result.FileExtension}");
    }

    private static bool TryParseType(string type, out ReportType reportType)
    {
        reportType = (type?.ToLowerInvariant()) switch
        {
            "by-court" => ReportType.ByCourt,
            "by-room" => ReportType.ByRoom,
            "by-copyist" => ReportType.ByCopyist,
            "by-reviewer" => ReportType.ByReviewer,
            "by-head" => ReportType.ByHead,
            "by-judge" => ReportType.ByJudge,
            "turnaround" => ReportType.Turnaround,
            "copies" => ReportType.Copies,
            _ => (ReportType)(-1),
        };
        return Enum.IsDefined(reportType);
    }
}
