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

    public async Task<IReadOnlyList<RankedCopyRef>> ListUnderReviewRankedBeforeAsync(
        IReadOnlyCollection<Guid> roomIds, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        await db.CopyRequests.AsNoTracking()
            .Where(x => roomIds.Contains(x.RoomId)
                && x.State == Domain.Enums.CopyState.UnderReview
                // same ranking as copyist acceptance: higher tier, or same tier but created earlier.
                && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc)))
            .OrderBy(x => x.Urgency).ThenBy(x => x.CreatedUtc) // highest-priority + oldest first = next to approve
            .Select(x => new RankedCopyRef(x.CopyNumber, x.MiscNumber))
            .ToListAsync(ct);

    public Task<bool> AnyUnprintedRankedBeforeAsync(
        IReadOnlyCollection<Guid> courtIds, bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        UnprintedRankedBeforeQuery(isApproved, urgency, createdUtc)
            .Where(x => courtIds.Contains(x.CourtId)).AnyAsync(ct);

    public Task<bool> AnyUnprintedRankedBeforeInRoomsAsync(
        IReadOnlyCollection<Guid> roomIds, bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        UnprintedRankedBeforeQuery(isApproved, urgency, createdUtc)
            .Where(x => roomIds.Contains(x.RoomId)).AnyAsync(ct);

    public Task<bool> AnyUnprintedRankedBeforeForCopyistAsync(
        Guid copyistId, bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        UnprintedRankedBeforeQuery(isApproved, urgency, createdUtc)
            .Where(x => x.AssignedCopyistId == copyistId).AnyAsync(ct);

    /// <summary>NOT-YET-PRINTED copies in the same print queue (approved vs draft) that rank BEFORE the
    /// given one (higher tier, or same tier but older). The caller adds the scope filter (court/room/copyist).</summary>
    private IQueryable<CopyRequest> UnprintedRankedBeforeQuery(bool isApproved, Domain.Enums.CaseUrgency urgency, DateTimeOffset createdUtc)
    {
        var q = db.CopyRequests.Where(x => x.PrintedUtc == null
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc)));
        // Approved and non-approved copies form independent print queues (each ordered on its own).
        return isApproved
            ? q.Where(x => x.State == Domain.Enums.CopyState.Approved)
            : q.Where(x => x.State != Domain.Enums.CopyState.Approved);
    }

    // FR-16: the only delete path. CopyContent cascades; AuditEntries have no FK/cascade → kept.
    public void Remove(CopyRequest request) => db.CopyRequests.Remove(request);
}
