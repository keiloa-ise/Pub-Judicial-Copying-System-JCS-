using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Api.Pdf;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Application.ReadModels;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// FR-15 print queues. The Reviewer and Copyist each have a queue of decisions awaiting print; the
/// selected decisions are marked printed (leaving the queue) and rendered into ONE merged PDF that the
/// browser prints directly. Role + court scoping is enforced inside <see cref="PrintQueueService"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/print-queue")]
public sealed class PrintQueueController(
    PrintQueueService queue, CopyRequestReadService readService, JudgmentPdfService pdfService) : ControllerBase
{
    /// <summary>FR-15: the reviewer's print queue (Approved, not-yet-printed) — priority-ordered.</summary>
    [HttpGet("reviewer")]
    public async Task<IActionResult> Reviewer(CancellationToken ct) =>
        Ok(await queue.GetReviewerQueueAsync(ct));

    /// <summary>FR-15: the copyist's print queue (accepted, in-preparation, not-yet-printed).</summary>
    [HttpGet("copyist")]
    public async Task<IActionResult> Copyist(CancellationToken ct) =>
        Ok(await queue.GetCopyistQueueAsync(ct));

    /// <summary>FR-15: mark the selected decisions printed and return them as ONE merged PDF.</summary>
    [HttpPost("print")]
    public async Task<IActionResult> Print(PrintManyRequest body, CancellationToken ct)
    {
        var ids = await queue.PrintManyAsync(new PrintManyCommand(body.Ids), ct);

        var details = new List<CopyRequestDetail>(ids.Count);
        foreach (var id in ids) details.Add(await readService.GetDetailAsync(id, ct));

        var bytes = pdfService.RenderMany(details);
        Response.Headers.ContentDisposition = "inline; filename=\"print-batch.pdf\"";
        return File(bytes, "application/pdf");
    }
}
