using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record SubmitForReviewCommand(Guid CopyRequestId);

/// <summary>FR-07 → FR-10: the assigned copyist submits the draft for review.</summary>
public sealed class SubmitForReviewService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    ICopyCorrectionStatStore correctionStats,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(SubmitForReviewCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetWithContentAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireRole(currentUser, Role.Copyist);
        Guard.RequireAssignedCourt(currentUser, request.CourtId);
        if (request.AssignedCopyistId != currentUser.Id)
            throw new ForbiddenException("Only the assigned copyist may submit this copy (BR-02).");

        // JC-58: if this submission closes an open return cycle, measure the words corrected since the
        // reviewer returned it (baseline captured on Return) and store the cycle's stat for the report.
        await RecordCorrectionAsync(request, ct);

        request.SubmitForReview(clock.UtcNow); // InPreparation → UnderReview
        audit.Append(request.Id, AuditAction.Submit);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task RecordCorrectionAsync(CopyRequest request, CancellationToken ct)
    {
        var baseline = await correctionStats.GetOpenReturnBaselineAsync(request.Id, ct);
        if (baseline is null) return; // first submission — nothing was returned yet

        var before = CorrectionMetrics.ExtractPlainText(baseline.SectionsJson);
        var after = CorrectionMetrics.ExtractPlainText(request.Content?.SectionsJson);
        var delta = CorrectionMetrics.Diff(before, after);
        var cycleIndex = await correctionStats.CountForCopyAsync(request.Id, ct);

        await correctionStats.AddAsync(CopyCorrectionStat.Create(
            request.Id, request.CourtId, request.AssignedCopyistId ?? currentUser.Id, baseline.ReviewerId,
            cycleIndex, baseline.ReturnedUtc, clock.UtcNow,
            delta.Added, delta.Removed, CorrectionMetrics.WordCount(after)), ct);
    }
}
