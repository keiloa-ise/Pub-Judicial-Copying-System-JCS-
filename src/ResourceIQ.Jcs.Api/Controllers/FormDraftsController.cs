using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Application.FormDrafts;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// JC-32: the authenticated user's recoverable form drafts. Every draft is scoped to the caller in the
/// service, so the {formKey} identifies only among the caller's own drafts.
/// </summary>
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
        var payloadJson = body.Payload.ValueKind == JsonValueKind.Undefined ? "{}" : body.Payload.GetRawText();
        var draft = await service.UpsertAsync(new UpsertFormDraftCommand(formKey, payloadJson, body.CopyRequestId), ct);
        return Ok(ToResponse(draft));
    }

    [HttpDelete("{formKey}")]
    public async Task<IActionResult> Delete(string formKey, CancellationToken ct)
    {
        await service.DeleteAsync(formKey, ct);
        return NoContent();
    }

    private static FormDraftResponse ToResponse(FormDraftResult d)
    {
        using var doc = JsonDocument.Parse(d.PayloadJson);
        return new FormDraftResponse(d.FormKey, d.Role, d.CopyRequestId, doc.RootElement.Clone(), d.UpdatedAt);
    }
}
