using System.Text.Json;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.FormDrafts;

public sealed record FormDraftResult(string FormKey, string Role, Guid? CopyRequestId, string PayloadJson, DateTimeOffset UpdatedAt);
public sealed record UpsertFormDraftCommand(string FormKey, string PayloadJson, Guid? CopyRequestId);

/// <summary>
/// JC-32: read/write a user's own recoverable form draft. Drafts are always scoped to
/// <see cref="ICurrentUser.Id"/> (a crafted form key can never reach another user's row). When a draft
/// is tied to a copy request, the caller's role + court + queue-state are verified so a user can only
/// draft copies they are actually allowed to edit.
/// </summary>
public sealed class FormDraftService(
    ICurrentUser currentUser,
    IClock clock,
    IFormDraftStore drafts,
    ICopyRequestRepository copyRequests,
    IUnitOfWork unitOfWork)
{
    public async Task<FormDraftResult?> GetAsync(string formKey, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        ValidateFormKey(formKey);
        var draft = await drafts.GetAsync(currentUser.Id, formKey.Trim(), ct);
        return draft is null ? null : ToResult(draft);
    }

    public async Task<FormDraftResult> UpsertAsync(UpsertFormDraftCommand cmd, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        ValidateFormKey(cmd.FormKey);
        ValidatePayload(cmd.PayloadJson);
        await EnsureCopyRequestAccessAsync(cmd.CopyRequestId, requireEditableState: true, ct);

        var formKey = cmd.FormKey.Trim();
        var now = clock.UtcNow;
        var draft = await drafts.GetAsync(currentUser.Id, formKey, ct);
        if (draft is null)
        {
            draft = FormDraft.Create(currentUser.Id, currentUser.Role.ToString(), formKey, cmd.CopyRequestId, cmd.PayloadJson, now);
            await drafts.AddAsync(draft, ct);
        }
        else
        {
            draft.Update(currentUser.Role.ToString(), formKey, cmd.CopyRequestId, cmd.PayloadJson, now);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return ToResult(draft);
    }

    public async Task DeleteAsync(string formKey, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        ValidateFormKey(formKey);
        var draft = await drafts.GetAsync(currentUser.Id, formKey.Trim(), ct);
        if (draft is null) return;
        drafts.Remove(draft);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureCopyRequestAccessAsync(Guid? copyRequestId, bool requireEditableState, CancellationToken ct)
    {
        if (copyRequestId is null) return; // untied draft (e.g. the create form) — own data only

        var request = await copyRequests.GetAsync(copyRequestId.Value, ct)
                      ?? throw new NotFoundException("Copy request not found.");
        if (currentUser.Role != Role.Administrator)
            Guard.RequireCopyScope(currentUser, request.CourtId, request.RoomId); // BR-06

        switch (currentUser.Role)
        {
            case Role.Copyist:
                if (request.AssignedCopyistId != currentUser.Id)
                    throw new ForbiddenException("Only the assigned copyist may draft this copy.");
                if (requireEditableState && request.State is not (CopyState.InPreparation or CopyState.Unlocked))
                    throw new ForbiddenException("Copyist drafts are allowed only while the copy is editable.");
                break;
            case Role.Reviewer:
                if (requireEditableState && request.State != CopyState.UnderReview)
                    throw new ForbiddenException("Reviewer drafts are allowed only while the copy is under review.");
                break;
            case Role.RegistryHead:
            case Role.Administrator:
                break;
            default:
                throw new ForbiddenException("Role is not permitted to draft this form.");
        }
    }

    private static void ValidateFormKey(string formKey)
    {
        if (string.IsNullOrWhiteSpace(formKey)) throw new DomainException("Form key is required.");
        if (formKey.Trim().Length > 200) throw new DomainException("Form key cannot exceed 200 characters.");
    }

    private static void ValidatePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new DomainException("Draft payload is required.");
        if (payloadJson.Length > FormDraft.MaxPayloadJsonLength) throw new DomainException("Draft payload is too large.");
        try { using var _ = JsonDocument.Parse(payloadJson); }
        catch (JsonException) { throw new DomainException("Draft payload must be valid JSON."); }
    }

    private static FormDraftResult ToResult(FormDraft d) =>
        new(d.FormKey, d.Role, d.CopyRequestId, d.PayloadJson, d.UpdatedUtc);
}
