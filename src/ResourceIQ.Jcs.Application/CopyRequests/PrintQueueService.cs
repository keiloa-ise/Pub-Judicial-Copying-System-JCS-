using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record PrintManyCommand(IReadOnlyList<Guid> CopyRequestIds);

/// <summary>
/// FR-15 print queues. Two role-specific queues of decisions awaiting print:
///   • Reviewer — Approved but not-yet-printed decisions in the reviewer's courts (cumulative selection).
///   • Copyist  — the copyist's ACCEPTED, in-preparation, not-yet-printed decisions (arbitrary selection).
/// Printing a selected set records each print (audited), which marks the copies printed so they leave
/// the queue; the API renders the selected decisions into one merged PDF.
/// </summary>
public sealed class PrintQueueService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    IJcsQueries queries,
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CopyRequestListItem>> GetReviewerQueueAsync(CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Reviewer);
        return queries.ListReviewerPrintQueueAsync(currentUser.CourtIds, ct);
    }

    public Task<IReadOnlyList<CopyRequestListItem>> GetCopyistQueueAsync(CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Copyist);
        return queries.ListCopyistPrintQueueAsync(currentUser.Id, ct);
    }

    /// <summary>Marks the selected queue items printed (audited) in one transaction. Returns the ids in
    /// the requested order so the caller can render them into a single merged PDF.</summary>
    public async Task<IReadOnlyList<Guid>> PrintManyAsync(PrintManyCommand cmd, CancellationToken ct)
    {
        if (cmd.CopyRequestIds is null || cmd.CopyRequestIds.Count == 0)
            throw new DomainException("لم يتم اختيار أي قرار للطباعة.");

        var ordered = cmd.CopyRequestIds.Distinct().ToList();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var id in ordered)
            {
                var request = await repository.GetAsync(id, token)
                              ?? throw new NotFoundException("Copy request not found.");
                AuthorizeQueueItem(request);
                request.MarkPrinted(currentUser.Id, clock.UtcNow);
                audit.Append(request.Id, AuditAction.Print);
            }
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
        return ordered;
    }

    /// <summary>Ensures the caller may print this copy from THEIR queue (role + court + queue membership).</summary>
    private void AuthorizeQueueItem(Domain.Entities.CopyRequest request)
    {
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06
        switch (currentUser.Role)
        {
            case Role.Reviewer:
                if (request.State != CopyState.Approved)
                    throw new ForbiddenException("Only approved decisions are in the reviewer print queue.");
                break;
            case Role.Copyist:
                if (request.AssignedCopyistId != currentUser.Id
                    || request.State != CopyState.InPreparation || request.AcceptedUtc is null)
                    throw new ForbiddenException("Only the copyist's accepted, in-preparation copies are printable here.");
                break;
            default:
                throw new ForbiddenException("Not permitted to print from a queue.");
        }
    }
}
