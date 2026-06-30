using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.Review;

public sealed record ApproveCommand(Guid CopyRequestId);
public sealed record ReturnCommand(Guid CopyRequestId, string Corrections);
public sealed record CorrectCommand(
    Guid CopyRequestId, Guid? FormTemplateId, string FieldValuesJson, string SectionsJson, string Body);

/// <summary>
/// FR-10/FR-11: a Reviewer approves (→ locked, read-only), corrects the content directly
/// (in place, while still under review), or returns a request for correction by the copyist.
/// Each action is atomic and writes an audit entry.
/// </summary>
public sealed class ReviewService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task ApproveAsync(ApproveCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Reviewer);            // BR-03
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06

        request.Approve(currentUser.Id, clock.UtcNow); // UnderReview → Approved (locked)
        audit.Append(request.Id, AuditAction.Approve);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// FR-10: the Reviewer corrects the content directly (in place) without bouncing the copy
    /// back to the copyist. The copy stays UnderReview; the Reviewer then approves it separately.
    /// The Reviewer role + court are enforced here; the edit is recorded as an append-only Edit
    /// audit entry whose actor is the reviewer (so the correction is attributable).
    /// </summary>
    public async Task CorrectAsync(CorrectCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetWithContentAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Reviewer);            // BR-03
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06

        var before = request.Content?.SectionsJson;
        var sectionsJson = RichText.SanitizeSectionsJson(cmd.SectionsJson);
        // CorrectByReviewer requires UnderReview; the copy is NOT moved off that state here.
        request.CorrectByReviewer(cmd.FormTemplateId, cmd.FieldValuesJson, sectionsJson, cmd.Body, clock.UtcNow);
        audit.Append(request.Id, AuditAction.Edit, beforeJson: before, afterJson: sectionsJson);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ReturnAsync(ReturnCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Reviewer);            // BR-03
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06

        if (string.IsNullOrWhiteSpace(cmd.Corrections))
            throw new DomainException("A reason / corrections note is required when returning a copy.");

        // UnderReview → InPreparation (7B). [OPEN] decision #2: no return-cycle cap yet.
        // The corrections are stored as the audit Reason so the copyist can see why it came back
        // (each return cycle adds its own append-only entry, preserving the full history).
        request.ReturnForCorrection(clock.UtcNow);
        audit.Append(request.Id, AuditAction.Return, reason: cmd.Corrections.Trim());
        await unitOfWork.SaveChangesAsync(ct);
    }
}
