using System.Text.Json;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.FormDrafts;

public sealed record FormDraftResult(
    string FormKey,
    string Role,
    Guid? CopyRequestId,
    string PayloadJson,
    DateTimeOffset UpdatedAt,
    string Source);

public sealed record UpsertFormDraftCommand(
    string FormKey,
    string PayloadJson,
    DateTimeOffset UpdatedAt,
    Guid? CopyRequestId);

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
        if (draft is null) return null;

        await EnsureCopyRequestAccessAsync(draft.CopyRequestId, requireActiveDraftState: true, ct);
        return ToResult(draft);
    }

    public async Task<FormDraftResult> UpsertAsync(UpsertFormDraftCommand cmd, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        ValidateFormKey(cmd.FormKey);
        ValidatePayload(cmd.PayloadJson);
        await EnsureCopyRequestAccessAsync(cmd.CopyRequestId, requireActiveDraftState: true, ct);

        var formKey = cmd.FormKey.Trim();
        var now = clock.UtcNow;
        var updatedAt = cmd.UpdatedAt == default ? now : cmd.UpdatedAt.ToUniversalTime();
        var draft = await drafts.GetAsync(currentUser.Id, formKey, ct);

        if (draft is null)
        {
            draft = FormDraft.Create(
                currentUser.Id,
                currentUser.Role.ToString(),
                formKey,
                cmd.CopyRequestId,
                cmd.PayloadJson,
                updatedAt,
                now);
            await drafts.AddAsync(draft, ct);
        }
        else
        {
            draft.Update(
                currentUser.Role.ToString(),
                formKey,
                cmd.CopyRequestId,
                cmd.PayloadJson,
                updatedAt,
                now);
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

        await EnsureCopyRequestAccessAsync(draft.CopyRequestId, requireActiveDraftState: false, ct);
        drafts.Remove(draft);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(int olderThanDays, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Administrator);
        if (olderThanDays < 1) throw new DomainException("olderThanDays must be at least 1.");

        var cutoff = clock.UtcNow.AddDays(-olderThanDays);
        return await drafts.DeleteOlderThanAsync(cutoff, ct);
    }

    private async Task EnsureCopyRequestAccessAsync(Guid? copyRequestId, bool requireActiveDraftState, CancellationToken ct)
    {
        if (copyRequestId is null) return;

        var request = await copyRequests.GetAsync(copyRequestId.Value, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        if (currentUser.Role != Role.Administrator)
            Guard.RequireAssignedCourt(currentUser, request.CourtId);

        switch (currentUser.Role)
        {
            case Role.Copyist:
                if (request.AssignedCopyistId != currentUser.Id)
                    throw new ForbiddenException("Only the assigned copyist may draft this copy.");
                if (requireActiveDraftState && request.State is not (CopyState.InPreparation or CopyState.Unlocked))
                    throw new ForbiddenException("Copyist drafts are allowed only while the copy is editable.");
                break;
            case Role.Reviewer:
                if (requireActiveDraftState && request.State != CopyState.UnderReview)
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
        if (string.IsNullOrWhiteSpace(formKey))
            throw new DomainException("Form key is required.");
        if (formKey.Trim().Length > 200)
            throw new DomainException("Form key cannot exceed 200 characters.");
    }

    private static void ValidatePayload(string payloadJson)
    {
        try { using var _ = JsonDocument.Parse(payloadJson); }
        catch (JsonException) { throw new DomainException("Draft payload must be valid JSON."); }
    }

    private static FormDraftResult ToResult(FormDraft draft) =>
        new(draft.FormKey, draft.Role, draft.CopyRequestId, draft.PayloadJson, draft.UpdatedUtc, "server");
}
