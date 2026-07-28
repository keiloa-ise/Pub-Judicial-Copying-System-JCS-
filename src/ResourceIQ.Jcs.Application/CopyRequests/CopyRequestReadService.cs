using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

/// <summary>
/// Read access to copy requests, scoped by the caller's role and court assignments (BR-06).
/// Controllers never pass scope; it is derived here so a user can only ever see their own slice.
/// </summary>
public sealed class CopyRequestReadService(
    ICurrentUser currentUser,
    IJcsQueries queries,
    IClock clock,
    ICopyNumberAllocator copyAllocator,
    IMiscNumberAllocator miscAllocator)
{
    /// <summary>Max page size for the work-queue listing — bounds the payload/DOM even when a client
    /// asks for more.</summary>
    public const int MaxListPageSize = 100;

    public Task<Paged<CopyRequestListItem>> ListForCurrentUserAsync(
        CopyRequestSearch search, int page, int pageSize, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxListPageSize ? 50 : pageSize;

        // FR-13: an explicit search state wins. Otherwise the reviewer's queue is UnderReview and the
        // copyist sees their own copies — but Approved copies appear in the copyist/reviewer queue ONLY
        // when SHOW_APPROVED_IN_QUEUE is true (env flag), to keep the working queue focused (default off).
        var showApproved = string.Equals(
            Environment.GetEnvironmentVariable("SHOW_APPROVED_IN_QUEUE"), "true", StringComparison.OrdinalIgnoreCase);
        IReadOnlyCollection<CopyState>? states;
        if (search.State is { } s) states = [s];
        else if (currentUser.Role == Role.Reviewer)
            states = showApproved ? [CopyState.UnderReview, CopyState.Approved] : [CopyState.UnderReview];
        else if (currentUser.Role == Role.Copyist && !showApproved)
            states = [CopyState.Created, CopyState.InPreparation, CopyState.UnderReview, CopyState.Unlocked];
        else states = null;

        // Court scope (BR-06). null => no court restriction (Administrator only). A non-null
        // (possibly empty) list restricts to those courts — an empty list matches nothing.
        var courtScope = ResolveCourtScope(search.CourtId);

        var filter = new CopyRequestFilter(
            States: states,
            AssignedCopyistId: currentUser.Role == Role.Copyist ? currentUser.Id : null,
            CreatedById: currentUser.Role == Role.RegistryHead ? currentUser.Id : null,
            CourtIds: courtScope,
            CopyNumber: search.CopyNumber,
            CaseBaseNumber: search.CaseBaseNumber,
            FromReservation: search.FromReservation,
            ToReservation: search.ToReservation);

        return queries.ListCopyRequestsAsync(filter, page, pageSize, ct);
    }

    /// <summary>
    /// Resolves the set of courts the listing may include. Administrators are unrestricted
    /// (null) unless they pick a specific court; everyone else is confined to their assigned
    /// courts, and an explicit court outside that set is rejected (BR-06).
    /// </summary>
    private IReadOnlyCollection<Guid>? ResolveCourtScope(Guid? requestedCourt)
    {
        if (currentUser.Role == Role.Administrator)
            return requestedCourt is { } ac ? [ac] : null;

        if (requestedCourt is { } cid)
        {
            if (!currentUser.IsAssignedToCourt(cid))
                throw new ForbiddenException("Not assigned to this court (BR-06).");
            return [cid];
        }
        return currentUser.CourtIds; // may be empty → matches nothing (safe)
    }

    public async Task<CopyRequestDetail> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var detail = await queries.GetCopyRequestAsync(id, ct)
                     ?? throw new NotFoundException("Copy request not found.");
        EnsureCanView(detail.CourtId);
        return detail;
    }

    /// <summary>FR-16: the Registry Head's deletion targets for the current year — the latest عادي
    /// copy per court, and the last متفرق per numbering scope (BR-09/BR-11).</summary>
    public Task<DeletionTargetsDto> ListDeletionTargetsAsync(CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);
        // RegistryHead is always court-scoped (BR-06); targets are for the current year.
        return queries.ListDeletionTargetsAsync(currentUser.CourtIds, clock.UtcNow.Year, ct);
    }

    /// <summary>Max originals returned per picker query — the list is filtered by room + search, so this
    /// cap keeps the payload bounded regardless of how many approved copies exist (500k+ safe).</summary>
    private const int OriginalsPageSize = 50;

    /// <summary>BR-11: Approved عادي copies the Registry Head may base a new متفرق on, filtered
    /// server-side to <paramref name="roomId"/> (+ optional <paramref name="search"/>). Court-scoped.</summary>
    public Task<IReadOnlyList<OriginalCopyOption>> ListSelectableOriginalsAsync(
        Guid roomId, string? search, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);
        return queries.ListSelectableOriginalsAsync(currentUser.CourtIds, roomId, search, OriginalsPageSize, ct);
    }

    /// <summary>Feature flag: batch print is available to the Registry Head (in addition to the
    /// Administrator) when ALLOW_HEAD_BATCH_PRINT is true (default true, read from .env).</summary>
    private static bool AllowHeadBatchPrint =>
        !string.Equals(Environment.GetEnvironmentVariable("ALLOW_HEAD_BATCH_PRINT"), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>FR-15 batch print: the copies in a court+room whose تاريخ الحجز falls within [from, to],
    /// of the chosen kind — مثبتة (Approved) or مسودة (any non-approved state). Available to the
    /// Administrator, and to the Registry Head (their courts) when ALLOW_HEAD_BATCH_PRINT is on.
    /// A read-only administrative export: NOT subject to the single-print order/once rules; never marks printed.</summary>
    public async Task<IReadOnlyList<CopyRequestListItem>> ListBatchPrintAsync(
        Guid courtId, Guid roomId, DateOnly from, DateOnly to, bool approved, CancellationToken ct)
    {
        if (currentUser.Role == Role.Administrator)
        {
            // unrestricted
        }
        else if (currentUser.Role == Role.RegistryHead && AllowHeadBatchPrint)
        {
            Guard.RequireAssignedCourt(currentUser, courtId); // BR-06: only their own courts
        }
        else
        {
            throw new ForbiddenException("Not permitted to batch-print.");
        }

        IReadOnlyCollection<CopyState> states = approved
            ? [CopyState.Approved]
            : [CopyState.Created, CopyState.InPreparation, CopyState.UnderReview, CopyState.Unlocked];
        var filter = new CopyRequestFilter(
            States: states, CourtIds: [courtId], RoomId: roomId, FromReservation: from, ToReservation: to);
        // A single court+room+date-range set is bounded; print ALL matching copies (no paging).
        return (await queries.ListCopyRequestsAsync(filter, 1, int.MaxValue, ct)).Items;
    }

    /// <summary>FR-03/FR-06: the last sequential number issued for a court/room scope in the current
    /// year — رقم النسخة for عادي, رقم المتفرق for متفرق — plus the number the next create will get.
    /// Lets the Registry Head see the running number before adding a decision. Court-scoped (BR-06).</summary>
    public async Task<LastNumberDto> GetLastIssuedNumberAsync(
        Guid courtId, Guid roomId, CaseCategory category, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);
        EnsureCanView(courtId); // BR-06: only within the head's assigned courts
        var year = clock.UtcNow.Year; // تاريخ الحجز is server-assigned = today, so numbering is the current year
        var last = category == CaseCategory.Miscellaneous
            ? await miscAllocator.PeekLastAsync(courtId, roomId, year, ct)
            : await copyAllocator.PeekLastAsync(courtId, roomId, year, ct);
        return new LastNumberDto(last, (last ?? 0) + 1);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid id, CancellationToken ct)
    {
        var detail = await queries.GetCopyRequestAsync(id, ct)
                     ?? throw new NotFoundException("Copy request not found.");
        EnsureCanView(detail.CourtId);
        return await queries.GetAuditAsync(id, ct);
    }

    private void EnsureCanView(Guid courtId)
    {
        Guard.RequireAuthenticated(currentUser);
        // Administrators see everything; everyone else only within their assigned courts (BR-06).
        if (currentUser.Role != Role.Administrator && !currentUser.IsAssignedToCourt(courtId))
            throw new ForbiddenException("Not permitted to view this copy request (BR-06).");
    }
}
