using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Application.Admin;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// Administrator-only configuration management (FR-02…FR-04, FR-08, FR-09). [Authorize] requires
/// a token; the AdminService re-checks the Administrator role on every call (never client-trusted).
/// Read lists for courts / paragraph / form templates live on the Lookups endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminController(AdminService admin) : ControllerBase
{
    // ── Courts ──
    [HttpGet("courts")]
    public async Task<IActionResult> ListCourts(CancellationToken ct) => Ok(await admin.ListAllCourtsAsync(ct));

    [HttpPost("courts")]
    public async Task<IActionResult> CreateCourt(CreateCourtRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateCourtAsync(b.Code, b.Name, ct) });

    [HttpPut("courts/{id:guid}")]
    public async Task<IActionResult> UpdateCourt(Guid id, UpdateCourtRequest b, CancellationToken ct)
    {
        await admin.UpdateCourtAsync(id, b.Name, b.IsActive, ct);
        return NoContent();
    }

    // ── Rooms (غرف) ──
    [HttpGet("rooms")]
    public async Task<IActionResult> ListRooms([FromQuery] Guid? courtId, CancellationToken ct) =>
        Ok(await admin.ListRoomsAsync(courtId, ct));

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateRoomAsync(b.CourtId, b.Code, b.Name, b.NumberingPolicy, b.NumberingLevel, b.CopyNumberingPolicy, ct) });

    [HttpPut("rooms/{id:guid}")]
    public async Task<IActionResult> UpdateRoom(Guid id, UpdateRoomRequest b, CancellationToken ct)
    {
        await admin.UpdateRoomAsync(id, b.Name, b.IsActive, b.NumberingPolicy, b.NumberingLevel, b.CopyNumberingPolicy, ct);
        return NoContent();
    }

    // ── Numbering start points (FR-17) ──
    [HttpGet("numbering/copy-counters")]
    public async Task<IActionResult> CopyCounters(CancellationToken ct) => Ok(await admin.ListCopyNumberCountersAsync(ct));

    [HttpPut("numbering/copy-counters")]
    public async Task<IActionResult> SetCopyCounter(SetCopyNumberStartRequest b, CancellationToken ct)
    { await admin.SetCopyNumberStartAsync(b.CourtId, b.RoomId, b.Year, b.LastNumber, ct); return NoContent(); }

    [HttpGet("numbering/misc-counters")]
    public async Task<IActionResult> MiscCounters(CancellationToken ct) => Ok(await admin.ListMiscNumberCountersAsync(ct));

    [HttpPut("numbering/misc-counters")]
    public async Task<IActionResult> SetMiscCounter(SetMiscNumberStartRequest b, CancellationToken ct)
    { await admin.SetMiscNumberStartAsync(b.CourtId, b.Scope, b.RoomId, b.Level, b.Year, b.LastNumber, ct); return NoContent(); }

    // ── Users ──
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(CancellationToken ct) => Ok(await admin.ListUsersAsync(ct));

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateUserAsync(b.Username, b.DisplayName, b.Role, b.Password, b.CourtIds, ct) });

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest b, CancellationToken ct)
    {
        await admin.UpdateUserAsync(id, b.DisplayName, b.Role, ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> SetUserActive(Guid id, SetActiveRequest b, CancellationToken ct)
    {
        await admin.SetUserActiveAsync(id, b.IsActive, ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest b, CancellationToken ct)
    {
        await admin.ResetPasswordAsync(id, b.Password, ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/courts")]
    public async Task<IActionResult> SetUserCourts(Guid id, SetCourtsRequest b, CancellationToken ct)
    {
        await admin.SetUserCourtsAsync(id, b.CourtIds, ct);
        return NoContent();
    }

    // ── Judges ──
    [HttpGet("judges")]
    public async Task<IActionResult> ListJudges(CancellationToken ct) => Ok(await admin.ListJudgesAsync(ct));

    [HttpPost("judges")]
    public async Task<IActionResult> CreateJudge(CreateJudgeRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateJudgeAsync(b.Name, b.RoomIds, ct) });

    [HttpPut("judges/{id:guid}")]
    public async Task<IActionResult> UpdateJudge(Guid id, UpdateJudgeRequest b, CancellationToken ct)
    {
        await admin.UpdateJudgeAsync(id, b.Name, b.IsActive, b.RoomIds, ct);
        return NoContent();
    }

    // ── Panel-member titles (صفات) ──
    [HttpGet("panel-titles")]
    public async Task<IActionResult> ListPanelTitles(CancellationToken ct) => Ok(await admin.ListPanelMemberTitlesAsync(ct));

    [HttpPost("panel-titles")]
    public async Task<IActionResult> CreatePanelTitle(CreatePanelTitleRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreatePanelMemberTitleAsync(b.Name, b.DisplayOrder, ct) });

    [HttpPut("panel-titles/{id:guid}")]
    public async Task<IActionResult> UpdatePanelTitle(Guid id, UpdatePanelTitleRequest b, CancellationToken ct)
    {
        await admin.UpdatePanelMemberTitleAsync(id, b.Name, b.IsActive, b.DisplayOrder, ct);
        return NoContent();
    }

    // ── Paragraph templates ──
    [HttpGet("paragraph-templates")]
    public async Task<IActionResult> ListParagraphs(CancellationToken ct) => Ok(await admin.ListAllParagraphsAsync(ct));

    [HttpPost("paragraph-templates")]
    public async Task<IActionResult> CreateParagraph(CreateParagraphRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateParagraphAsync(b.Title, b.Body, b.FormTemplateId, ct) });

    [HttpPut("paragraph-templates/{id:guid}")]
    public async Task<IActionResult> UpdateParagraph(Guid id, UpdateParagraphRequest b, CancellationToken ct)
    {
        await admin.UpdateParagraphAsync(id, b.Title, b.Body, b.IsArchived, b.FormTemplateId, ct);
        return NoContent();
    }

    // ── Form templates ──
    [HttpGet("form-templates")]
    public async Task<IActionResult> ListFormTemplates(CancellationToken ct) => Ok(await admin.ListAllFormTemplatesAsync(ct));

    [HttpPost("form-templates")]
    public async Task<IActionResult> CreateFormTemplate(CreateFormTemplateRequest b, CancellationToken ct) =>
        Ok(new { id = await admin.CreateFormTemplateAsync(b.Name, b.Fields, ct) });

    [HttpPut("form-templates/{id:guid}")]
    public async Task<IActionResult> UpdateFormTemplate(Guid id, UpdateFormTemplateRequest b, CancellationToken ct)
    {
        await admin.UpdateFormTemplateAsync(id, b.Name, b.IsActive, b.Fields, ct);
        return NoContent();
    }
}
