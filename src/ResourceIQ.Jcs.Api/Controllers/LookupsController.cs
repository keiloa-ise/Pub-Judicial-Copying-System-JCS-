using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Application.Lookups;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>Reference data for forms (courts, copyists, paragraph/form templates).</summary>
[ApiController]
[Authorize]
[Route("api/lookups")]
public sealed class LookupsController(LookupService lookups) : ControllerBase
{
    [HttpGet("courts")]
    public async Task<IActionResult> Courts(CancellationToken ct) =>
        Ok(await lookups.CourtsForCurrentUserAsync(ct));

    [HttpGet("courts/{courtId:guid}/copyists")]
    public async Task<IActionResult> Copyists(Guid courtId, CancellationToken ct) =>
        Ok(await lookups.CopyistsInCourtAsync(courtId, ct));

    [HttpGet("courts/{courtId:guid}/rooms")]
    public async Task<IActionResult> Rooms(Guid courtId, CancellationToken ct) =>
        Ok(await lookups.RoomsInCourtAsync(courtId, ct));

    [HttpGet("rooms/{roomId:guid}/judges")]
    public async Task<IActionResult> Judges(Guid roomId, CancellationToken ct) =>
        Ok(await lookups.JudgesInRoomAsync(roomId, ct));

    [HttpGet("panel-titles")]
    public async Task<IActionResult> PanelTitles(CancellationToken ct) =>
        Ok(await lookups.PanelMemberTitlesAsync(ct));

    [HttpGet("paragraph-templates")]
    public async Task<IActionResult> Paragraphs([FromQuery] Guid? formTemplateId, CancellationToken ct) =>
        Ok(await lookups.InsertableParagraphsAsync(formTemplateId, ct));

    [HttpGet("form-templates")]
    public async Task<IActionResult> Forms(CancellationToken ct) =>
        Ok(await lookups.ActiveFormTemplatesAsync(ct));
}
