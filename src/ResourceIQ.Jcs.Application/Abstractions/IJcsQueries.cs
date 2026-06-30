using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Read-only queries (projections to DTOs). Implemented in Infrastructure over EF.</summary>
public interface IJcsQueries
{
    Task<IReadOnlyList<CopyRequestListItem>> ListCopyRequestsAsync(CopyRequestFilter filter, CancellationToken ct);
    Task<CopyRequestDetail?> GetCopyRequestAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid copyRequestId, CancellationToken ct);

    /// <summary>Id of the most-recently-created copy request within the given courts. Null courts =
    /// unrestricted (admin); an empty list yields null.</summary>
    Task<Guid?> GetLatestCopyRequestIdAsync(IReadOnlyCollection<Guid>? courtIds, CancellationToken ct);

    /// <summary>FR-16: the Registry Head's deletion targets for the year — the latest عادي copy per
    /// court, and the last متفرق per numbering scope (BR-09/BR-11).</summary>
    Task<DeletionTargetsDto> ListDeletionTargetsAsync(
        IReadOnlyCollection<Guid>? courtIds, int year, CancellationToken ct);

    /// <summary>BR-11: Approved عادي copies (within the given courts) selectable as a متفرق's original.</summary>
    Task<IReadOnlyList<OriginalCopyOption>> ListSelectableOriginalsAsync(
        IReadOnlyCollection<Guid>? courtIds, CancellationToken ct);

    /// <summary>FR-17: existing رقم النسخة and رقم المتفرق start-point counters (for the admin screen).</summary>
    Task<IReadOnlyList<CopyNumberCounterDto>> ListCopyNumberCountersAsync(CancellationToken ct);
    Task<IReadOnlyList<MiscNumberCounterDto>> ListMiscNumberCountersAsync(CancellationToken ct);

    // Lookups
    Task<IReadOnlyList<CourtDto>> ListCourtsAsync(IReadOnlyCollection<Guid>? restrictTo, bool activeOnly, CancellationToken ct);
    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? courtId, bool activeOnly, CancellationToken ct);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct);
    Task<IReadOnlyList<LookupItem>> ListUsersByRoleAndCourtAsync(Role role, Guid courtId, CancellationToken ct);
    Task<IReadOnlyList<LookupItem>> ListJudgesByRoomAsync(Guid roomId, CancellationToken ct);
    /// <summary>Active panel-member titles (صفات), in display order, for the copy editor's pickers.</summary>
    Task<IReadOnlyList<LookupItem>> ListPanelMemberTitlesAsync(CancellationToken ct);
    Task<IReadOnlyList<ParagraphTemplateDto>> ListParagraphTemplatesAsync(
        bool includeArchived, Guid? formTemplateId, bool onlyForTemplate, CancellationToken ct);
    Task<IReadOnlyList<FormTemplateDto>> ListFormTemplatesAsync(bool activeOnly, CancellationToken ct);
}
