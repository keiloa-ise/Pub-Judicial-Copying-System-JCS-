using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

public sealed class CopyRequestRepository(JcsDbContext db) : ICopyRequestRepository
{
    public Task<CopyRequest?> GetAsync(Guid id, CancellationToken ct) =>
        db.CopyRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CopyRequest?> GetWithContentAsync(Guid id, CancellationToken ct) =>
        db.CopyRequests.Include(x => x.Content).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(CopyRequest request, CancellationToken ct) =>
        await db.CopyRequests.AddAsync(request, ct);

    public Task<bool> AnyLinkedMiscAsync(Guid originalCopyId, CancellationToken ct) =>
        db.CopyRequests.AnyAsync(x => x.OriginalCopyId == originalCopyId, ct);

    public Task<bool> NormalCaseBaseExistsAsync(Guid courtId, string caseBaseNumber, CancellationToken ct) =>
        db.CopyRequests.AnyAsync(x => x.CourtId == courtId
            && x.Category == Domain.Enums.CaseCategory.Normal && x.CaseBaseNumber == caseBaseNumber, ct);

    public Task<bool> AnyUnacceptedRankedBeforeAsync(
        Guid copyistId, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        db.CopyRequests.AnyAsync(x => x.AssignedCopyistId == copyistId
            && x.State == Domain.Enums.CopyState.InPreparation && x.AcceptedUtc == null
            // higher tier (lower enum value = higher priority), OR same tier but created earlier (oldest-first).
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc)), ct);

    public Task<bool> AnyUnderReviewRankedBeforeAsync(
        IReadOnlyCollection<Guid> courtIds, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        db.CopyRequests.AnyAsync(x => courtIds.Contains(x.CourtId)
            && x.State == Domain.Enums.CopyState.UnderReview
            // same ranking as copyist acceptance: higher tier, or same tier but created earlier.
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc)), ct);

    public Task<bool> AnyUnprintedRankedBeforeAsync(
        IReadOnlyCollection<Guid> courtIds, bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct)
    {
        var q = db.CopyRequests.Where(x => courtIds.Contains(x.CourtId)
            && x.PrintedUtc == null
            // same ranking as acceptance/approval: higher tier, or same tier but created earlier.
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc)));
        // Approved and non-approved copies form independent print queues (each ordered on its own).
        q = isApproved
            ? q.Where(x => x.State == Domain.Enums.CopyState.Approved)
            : q.Where(x => x.State != Domain.Enums.CopyState.Approved);
        return q.AnyAsync(ct);
    }

    // FR-16: the only delete path. CopyContent cascades; AuditEntries have no FK/cascade → kept.
    public void Remove(CopyRequest request) => db.CopyRequests.Remove(request);
}
