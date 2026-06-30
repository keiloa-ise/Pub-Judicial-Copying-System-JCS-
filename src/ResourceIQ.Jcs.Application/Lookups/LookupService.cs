using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Lookups;

/// <summary>Reference data for forms: the courts a user may act in, and the copyists in a court.</summary>
public sealed class LookupService(ICurrentUser currentUser, IJcsQueries queries)
{
    /// <summary>Active courts the caller may use. Admin → all active; others → assigned only (BR-06).</summary>
    public Task<IReadOnlyList<CourtDto>> CourtsForCurrentUserAsync(CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        var restrict = currentUser.Role == Role.Administrator ? null : currentUser.CourtIds;
        return queries.ListCourtsAsync(restrict, activeOnly: true, ct);
    }

    /// <summary>Copyists assigned to a court (for the create-request assignee picker).</summary>
    public Task<IReadOnlyList<LookupItem>> CopyistsInCourtAsync(Guid courtId, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        if (currentUser.Role != Role.Administrator && !currentUser.IsAssignedToCourt(courtId))
            throw new ForbiddenException("Not assigned to this court (BR-06).");
        return queries.ListUsersByRoleAndCourtAsync(Role.Copyist, courtId, ct);
    }

    /// <summary>Active rooms (غرف) of a court the caller may use (for the create-request room picker).</summary>
    public Task<IReadOnlyList<RoomDto>> RoomsInCourtAsync(Guid courtId, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        if (currentUser.Role != Role.Administrator && !currentUser.IsAssignedToCourt(courtId))
            throw new ForbiddenException("Not assigned to this court (BR-06).");
        return queries.ListRoomsAsync(courtId, activeOnly: true, ct);
    }

    /// <summary>Active judges assigned to a room (for the judgment panel pickers). Court scope is
    /// derived from the room's court (BR-06).</summary>
    public async Task<IReadOnlyList<LookupItem>> JudgesInRoomAsync(Guid roomId, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        var room = await queries.GetRoomAsync(roomId, ct)
                   ?? throw new ForbiddenException("Room not found.");
        if (currentUser.Role != Role.Administrator && !currentUser.IsAssignedToCourt(room.CourtId))
            throw new ForbiddenException("Not assigned to this court (BR-06).");
        return await queries.ListJudgesByRoomAsync(roomId, ct);
    }

    /// <summary>Active panel-member titles (صفات) the copyist picks per panel member while editing.</summary>
    public Task<IReadOnlyList<LookupItem>> PanelMemberTitlesAsync(CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        return queries.ListPanelMemberTitlesAsync(ct);
    }

    /// <summary>Non-archived paragraphs a copyist may insert for a given form type (FR-09).</summary>
    public Task<IReadOnlyList<ParagraphTemplateDto>> InsertableParagraphsAsync(Guid? formTemplateId, CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        return queries.ListParagraphTemplatesAsync(includeArchived: false, formTemplateId, onlyForTemplate: true, ct);
    }

    public Task<IReadOnlyList<FormTemplateDto>> ActiveFormTemplatesAsync(CancellationToken ct)
    {
        Guard.RequireAuthenticated(currentUser);
        return queries.ListFormTemplatesAsync(activeOnly: true, ct);
    }
}
