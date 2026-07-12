using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Application.Admin;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Api.Pdf;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Review;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// Copy-request workflow. [Authorize] requires a valid JWT; the per-action
/// role/court rules (BR-01…BR-06) are re-checked inside each service — the client is never
/// trusted. Returns are plumbed through ReviewService; unlock through UnlockService (admin).
/// </summary>
[ApiController]
[Authorize]
[Route("api/copy-requests")]
public sealed class CopyRequestsController(
    CreateCopyRequestService createService,
    DeleteCopyService deleteService,
    AcceptCopyService acceptService,
    ExpediteCopyService expediteService,
    PrepareCopyService prepareService,
    SubmitForReviewService submitService,
    ReviewService reviewService,
    UnlockService unlockService,
    CopyRequestReadService readService,
    JudgmentPdfService pdfService) : ControllerBase
{
    /// <summary>
    /// List requests visible to the caller (always role + court scoped). Optional advanced-search
    /// query params narrow within that scope: state, copyNumber, caseBaseNumber, courtId, and a
    /// reservation-date range (fromReservation / toReservation, yyyy-MM-dd).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] CopyState? state,
        [FromQuery] string? copyNumber,
        [FromQuery] string? caseBaseNumber,
        [FromQuery] Guid? courtId,
        [FromQuery] DateOnly? fromReservation,
        [FromQuery] DateOnly? toReservation,
        CancellationToken ct)
    {
        var search = new CopyRequestSearch(state, copyNumber, caseBaseNumber, courtId, fromReservation, toReservation);
        return Ok(await readService.ListForCurrentUserAsync(search, ct));
    }

    /// <summary>Full detail (incl. content) for one request, if the caller may view it.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await readService.GetDetailAsync(id, ct));

    /// <summary>
    /// FR-15: server-rendered PDF of the judgment ("إعلام الحكم"), returned inline. The document
    /// is built on the server from the authoritative record, so it cannot be edited in the browser
    /// before printing. Same view authorization as <see cref="Get"/> (court-scoped, BR-06).
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var detail = await readService.GetDetailAsync(id, ct);
        var bytes = pdfService.Render(detail);
        var name = $"judgment-{(detail.CopyNumber ?? id.ToString()).Replace('/', '-')}.pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
        return File(bytes, "application/pdf");
    }

    /// <summary>Append-only audit history for one request (read).</summary>
    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> Audit(Guid id, CancellationToken ct) =>
        Ok(await readService.GetAuditAsync(id, ct));

    /// <summary>FR-06: Registry Head creates a request (number allocated server-side).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateCopyRequestRequest body, CancellationToken ct)
    {
        var req = await createService.HandleAsync(new CreateCopyRequestCommand(
            body.CourtId, body.RoomId, body.CaseFilingDate, body.CaseBaseNumber,
            body.Category, body.Urgency, body.ExpediteRequestNumber, body.ReferenceNumber, body.AssignedCopyistId,
            body.OriginalCopyId), ct);
        return CreatedAtAction(nameof(Create), new { id = req.Id }, new { req.Id, req.CopyNumber, state = req.State.ToString() });
    }

    /// <summary>FR-16: deletion targets for the head's courts (current year) — latest عادي per court
    /// and last متفرق per numbering scope — populates the Registry Head's deletion window.</summary>
    [HttpGet("deletion-targets")]
    public async Task<IActionResult> DeletionTargets(CancellationToken ct) =>
        Ok(await readService.ListDeletionTargetsAsync(ct));

    /// <summary>BR-11: Approved عادي copies a Registry Head may base a new متفرق on (the original picker).</summary>
    [HttpGet("originals")]
    public async Task<IActionResult> Originals(CancellationToken ct) =>
        Ok(await readService.ListSelectableOriginalsAsync(ct));

    /// <summary>FR-16: Registry Head deletes the latest copy in a court regardless of type (hard
    /// delete; رقم النسخة — and رقم المتفرق if متفرق — rolled back so no gap; audit kept).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await deleteService.DeleteAsync(new DeleteCopyRequestCommand(id), ct);
        return NoContent();
    }

    /// <summary>FR-07: assigned Copyist saves draft content.</summary>
    [HttpPut("{id:guid}/content")]
    public async Task<IActionResult> SaveDraft(Guid id, SaveDraftRequest body, CancellationToken ct)
    {
        await prepareService.SaveDraftAsync(
            new SaveDraftCommand(id, body.FormTemplateId, body.FieldValuesJson, body.SectionsJson, body.DissentSectionsJson, body.RebuttalSectionsJson, body.Body), ct);
        return NoContent();
    }

    /// <summary>FR-07: assigned Copyist accepts the copy (must precede editing; priority-ordered).</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        await acceptService.HandleAsync(new AcceptCopyCommand(id), ct);
        return NoContent();
    }

    /// <summary>FR-06: Registry Head escalates a non-approved copy to مستعجل (expedite number required).</summary>
    [HttpPost("{id:guid}/expedite")]
    public async Task<IActionResult> Expedite(Guid id, ExpediteRequest body, CancellationToken ct)
    {
        await expediteService.HandleAsync(new ExpediteCopyCommand(id, body.ExpediteRequestNumber), ct);
        return NoContent();
    }

    /// <summary>FR-07 → FR-10: Copyist submits for review.</summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await submitService.HandleAsync(new SubmitForReviewCommand(id), ct);
        return NoContent();
    }

    /// <summary>FR-10/11: Reviewer approves → locked.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await reviewService.ApproveAsync(new ApproveCommand(id), ct);
        return NoContent();
    }

    /// <summary>FR-10: Reviewer corrects the content directly (in place, stays under review).</summary>
    [HttpPut("{id:guid}/correct")]
    public async Task<IActionResult> Correct(Guid id, SaveDraftRequest body, CancellationToken ct)
    {
        await reviewService.CorrectAsync(
            new CorrectCommand(id, body.FormTemplateId, body.FieldValuesJson, body.SectionsJson, body.DissentSectionsJson, body.RebuttalSectionsJson, body.Body), ct);
        return NoContent();
    }

    /// <summary>FR-10: Reviewer returns for correction (7B).</summary>
    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, ReturnRequest body, CancellationToken ct)
    {
        await reviewService.ReturnAsync(new ReturnCommand(id, body.Corrections), ct);
        return NoContent();
    }

    /// <summary>FR-12: Administrator unlocks an approved copy (reason mandatory, audited).</summary>
    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, UnlockRequest body, CancellationToken ct)
    {
        await unlockService.HandleAsync(new UnlockCommand(id, body.Reason), ct);
        return NoContent();
    }
}
