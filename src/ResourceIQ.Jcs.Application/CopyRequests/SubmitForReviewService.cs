using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record SubmitForReviewCommand(Guid CopyRequestId);

/// <summary>FR-07 → FR-10: the assigned copyist submits the draft for review.</summary>
public sealed class SubmitForReviewService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(SubmitForReviewCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Copyist);
        Guard.RequireAssignedCourt(currentUser, request.CourtId);
        if (request.AssignedCopyistId != currentUser.Id)
            throw new ForbiddenException("Only the assigned copyist may submit this copy (BR-02).");

        request.SubmitForReview(clock.UtcNow); // InPreparation → UnderReview
        audit.Append(request.Id, AuditAction.Submit);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
