using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Application.FormDrafts;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/form-drafts")]
public sealed class FormDraftsController(FormDraftService service) : ControllerBase
{
    [HttpGet("{formKey}")]
    public async Task<IActionResult> Get(string formKey, CancellationToken ct)
    {
        var draft = await service.GetAsync(formKey, ct);
        return Ok(draft is null ? null : ToResponse(draft));
    }

    [HttpPut("{formKey}")]
    public async Task<IActionResult> Upsert(string formKey, FormDraftRequest body, CancellationToken ct)
    {
        var payloadJson = body.Payload.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : body.Payload.GetRawText();
        var draft = await service.UpsertAsync(
            new UpsertFormDraftCommand(formKey, payloadJson, body.UpdatedAt, body.CopyRequestId),
            ct);
        return Ok(ToResponse(draft));
    }

    [HttpDelete("{formKey}")]
    public async Task<IActionResult> Delete(string formKey, CancellationToken ct)
    {
        await service.DeleteAsync(formKey, ct);
        return NoContent();
    }

    [Authorize(Roles = nameof(Role.Administrator))]
    [HttpDelete("admin/old")]
    public async Task<IActionResult> DeleteOld([FromQuery] int olderThanDays = 30, CancellationToken ct = default)
    {
        var deleted = await service.DeleteOlderThanAsync(olderThanDays, ct);
        return Ok(new { deleted });
    }

    private static FormDraftResponse ToResponse(FormDraftResult draft)
    {
        using var doc = JsonDocument.Parse(draft.PayloadJson);
        return new FormDraftResponse(
            draft.FormKey,
            draft.Role,
            draft.CopyRequestId,
            doc.RootElement.Clone(),
            draft.UpdatedAt,
            draft.Source);
    }
}
