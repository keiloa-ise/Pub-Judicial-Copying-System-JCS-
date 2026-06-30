using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record SaveDraftCommand(
    Guid CopyRequestId, Guid? FormTemplateId, string FieldValuesJson, string SectionsJson, string Body);

/// <summary>
/// FR-07: the assigned copyist edits content and saves drafts (any number of times). Edits
/// are only permitted while the request is in preparation — the domain enforces BR-04.
/// </summary>
public sealed class PrepareCopyService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task SaveDraftAsync(SaveDraftCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetWithContentAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Copyist);          // BR-02
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06
        if (request.AssignedCopyistId != currentUser.Id)
            throw new ForbiddenException("Only the assigned copyist may edit this copy (BR-02).");

        var before = request.Content?.SectionsJson;
        // Section text may carry inline bold/italic — reduce it to the safe formatting subset
        // (client input is never trusted) before persisting the legally-significant content.
        var sectionsJson = RichText.SanitizeSectionsJson(cmd.SectionsJson);
        // EnsureEditable → BR-04. The dynamic body is now an ordered list of inserted sections.
        request.UpdateContent(cmd.FormTemplateId, cmd.FieldValuesJson, sectionsJson, cmd.Body, clock.UtcNow);

        audit.Append(request.Id, AuditAction.Edit, beforeJson: before, afterJson: sectionsJson);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
