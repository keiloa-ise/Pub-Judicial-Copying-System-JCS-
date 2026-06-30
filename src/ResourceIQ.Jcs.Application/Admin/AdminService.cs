using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.Admin;

/// <summary>
/// All administrator-only management (FR-02…FR-04, FR-08, FR-09). Every method first asserts
/// the caller is an Administrator (BR-05 sibling); the store performs no auth of its own.
/// </summary>
public sealed class AdminService(
    ICurrentUser currentUser, IAdminStore store, IJcsQueries queries, IPasswordHasher passwordHasher)
{
    private void RequireAdmin() => Guard.RequireRole(currentUser, Role.Administrator);

    // ── Full read lists (admin sees inactive/archived too) ──
    public Task<IReadOnlyList<CourtDto>> ListAllCourtsAsync(CancellationToken ct)
    { RequireAdmin(); return queries.ListCourtsAsync(null, activeOnly: false, ct); }

    public Task<IReadOnlyList<ParagraphTemplateDto>> ListAllParagraphsAsync(CancellationToken ct)
    { RequireAdmin(); return queries.ListParagraphTemplatesAsync(includeArchived: true, null, onlyForTemplate: false, ct); }

    public Task<IReadOnlyList<FormTemplateDto>> ListAllFormTemplatesAsync(CancellationToken ct)
    { RequireAdmin(); return queries.ListFormTemplatesAsync(activeOnly: false, ct); }

    // ── Courts ──
    public async Task<Guid> CreateCourtAsync(string code, string name, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new DomainException("Court code and name are required.");
        if (await store.CourtCodeExistsAsync(code.Trim(), ct))
            throw new DomainException("Court code already exists (FR-03).");
        return await store.CreateCourtAsync(code.Trim(), name.Trim(), ct);
    }

    public Task UpdateCourtAsync(Guid id, string name, bool isActive, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Court name is required.");
        return store.UpdateCourtAsync(id, name.Trim(), isActive, ct);
    }

    // ── Rooms (غرف) ──
    public Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? courtId, CancellationToken ct)
    { RequireAdmin(); return queries.ListRoomsAsync(courtId, activeOnly: false, ct); }

    public async Task<Guid> CreateRoomAsync(
        Guid courtId, string code, string name, NumberingPolicy policy, string? level, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room code and name are required.");
        if (!await store.CourtExistsAsync(courtId, ct))
            throw new DomainException("Court not found.");
        if (await store.RoomCodeExistsInCourtAsync(courtId, code.Trim(), ct))
            throw new DomainException("Room code already exists in this court.");
        return await store.CreateRoomAsync(courtId, code.Trim(), name.Trim(), policy, NormalizeLevel(policy, level), ct);
    }

    public Task UpdateRoomAsync(Guid id, string name, bool isActive, NumberingPolicy policy, string? level, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Room name is required.");
        return store.UpdateRoomAsync(id, name.Trim(), isActive, policy, NormalizeLevel(policy, level), ct);
    }

    /// <summary>A Special-policy room needs a level letter A..Z; other policies carry no level.</summary>
    private static string? NormalizeLevel(NumberingPolicy policy, string? level)
    {
        if (policy != NumberingPolicy.Special) return null;
        var l = level?.Trim().ToUpperInvariant();
        if (l is not { Length: 1 } || l[0] < 'A' || l[0] > 'Z')
            throw new DomainException("المستوى الخاص يجب أن يكون حرفاً من A إلى Z.");
        return l;
    }

    // ── Numbering start points (FR-17): the Administrator seeds the "last issued number" at go-live
    //    so the auto numbering continues at +1. The store guards against lowering below numbers
    //    already used in the system. ──
    public Task<IReadOnlyList<CopyNumberCounterDto>> ListCopyNumberCountersAsync(CancellationToken ct)
    { RequireAdmin(); return queries.ListCopyNumberCountersAsync(ct); }

    public Task<IReadOnlyList<MiscNumberCounterDto>> ListMiscNumberCountersAsync(CancellationToken ct)
    { RequireAdmin(); return queries.ListMiscNumberCountersAsync(ct); }

    public async Task SetCopyNumberStartAsync(Guid courtId, int year, int lastNumber, CancellationToken ct)
    {
        RequireAdmin();
        ValidateYearAndNumber(year, lastNumber);
        if (!await store.CourtExistsAsync(courtId, ct)) throw new DomainException("Court not found.");
        await store.SetCopyNumberStartAsync(courtId, year, lastNumber, ct);
    }

    public async Task SetMiscNumberStartAsync(
        Guid courtId, NumberingPolicy scope, Guid? roomId, string? level, int year, int lastNumber, CancellationToken ct)
    {
        RequireAdmin();
        ValidateYearAndNumber(year, lastNumber);
        if (!await store.CourtExistsAsync(courtId, ct)) throw new DomainException("Court not found.");

        string scopeKey;
        switch (scope)
        {
            case NumberingPolicy.Court:
                scopeKey = Room.ScopeKey(NumberingPolicy.Court, courtId, Guid.Empty, null);
                break;
            case NumberingPolicy.Room:
                if (roomId is not { } rid) throw new DomainException("يجب اختيار الغرفة لمستوى الغرفة.");
                if (!await store.RoomsExistAsync([rid], ct)) throw new DomainException("Room not found.");
                scopeKey = Room.ScopeKey(NumberingPolicy.Room, courtId, rid, null);
                break;
            case NumberingPolicy.Special:
                scopeKey = Room.ScopeKey(NumberingPolicy.Special, courtId, Guid.Empty, NormalizeLevel(NumberingPolicy.Special, level));
                break;
            default:
                throw new DomainException("نطاق ترقيم غير صالح.");
        }
        await store.SetMiscNumberStartAsync(courtId, scopeKey, year, lastNumber, ct);
    }

    private static void ValidateYearAndNumber(int year, int lastNumber)
    {
        if (year is < 2000 or > 3000) throw new DomainException("سنة غير صالحة.");
        if (lastNumber < 0) throw new DomainException("الرقم يجب أن يكون صفراً أو أكثر.");
    }

    // ── Users ──
    public Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct)
    {
        RequireAdmin();
        return store.ListUsersAsync(ct);
    }

    public async Task<Guid> CreateUserAsync(
        string username, string displayName, Role role, string password,
        IReadOnlyCollection<Guid> courtIds, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new DomainException("Username and password are required.");
        if (await store.UsernameExistsAsync(username.Trim(), ct))
            throw new DomainException("Username already exists.");

        var hash = passwordHasher.Hash(password);
        return await store.CreateUserAsync(username.Trim(), displayName.Trim(), role, hash, courtIds, ct);
    }

    public async Task UpdateUserAsync(Guid id, string displayName, Role role, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Display name is required.");
        // Guard against an admin locking themselves out by removing their own admin role.
        if (id == currentUser.Id && role != Role.Administrator)
            throw new DomainException("You cannot change your own role away from Administrator.");
        await store.UpdateUserAsync(id, displayName.Trim(), role, ct);
    }

    public Task SetUserActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        RequireAdmin();
        if (id == currentUser.Id && !active)
            throw new DomainException("You cannot disable your own account.");
        return store.SetUserActiveAsync(id, active, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new DomainException("New password is required.");
        await store.SetPasswordHashAsync(id, passwordHasher.Hash(newPassword), ct);
    }

    public Task SetUserCourtsAsync(Guid id, IReadOnlyCollection<Guid> courtIds, CancellationToken ct)
    {
        RequireAdmin();
        return store.SetUserCourtsAsync(id, courtIds, ct);
    }

    // ── Judges ──
    public Task<IReadOnlyList<JudgeDto>> ListJudgesAsync(CancellationToken ct)
    {
        RequireAdmin();
        return store.ListJudgesAsync(ct);
    }

    public async Task<Guid> CreateJudgeAsync(string name, IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Judge name is required.");
        if (roomIds is null || roomIds.Count == 0)
            throw new DomainException("A judge must be assigned to at least one room.");
        if (!await store.RoomsExistAsync(roomIds, ct))
            throw new DomainException("One or more selected rooms do not exist.");
        return await store.CreateJudgeAsync(name.Trim(), roomIds, ct);
    }

    public async Task UpdateJudgeAsync(Guid id, string name, bool isActive, IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Judge name is required.");
        if (roomIds is null || roomIds.Count == 0)
            throw new DomainException("A judge must be assigned to at least one room.");
        if (!await store.RoomsExistAsync(roomIds, ct))
            throw new DomainException("One or more selected rooms do not exist.");
        await store.UpdateJudgeAsync(id, name.Trim(), isActive, roomIds, ct);
    }

    // ── Panel-member titles (صفات) ──
    public Task<IReadOnlyList<PanelMemberTitleDto>> ListPanelMemberTitlesAsync(CancellationToken ct)
    {
        RequireAdmin();
        return store.ListPanelMemberTitlesAsync(ct);
    }

    public Task<Guid> CreatePanelMemberTitleAsync(string name, int displayOrder, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("صفة العضو مطلوبة.");
        return store.CreatePanelMemberTitleAsync(name.Trim(), displayOrder, ct);
    }

    public Task UpdatePanelMemberTitleAsync(Guid id, string name, bool isActive, int displayOrder, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("صفة العضو مطلوبة.");
        return store.UpdatePanelMemberTitleAsync(id, name.Trim(), isActive, displayOrder, ct);
    }

    // ── Paragraph templates (FR-09) ──
    public async Task<Guid> CreateParagraphAsync(string title, string body, Guid? formTemplateId, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Paragraph title is required.");
        return await store.CreateParagraphAsync(title.Trim(), body ?? string.Empty, formTemplateId, ct);
    }

    public Task UpdateParagraphAsync(Guid id, string title, string body, bool isArchived, Guid? formTemplateId, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Paragraph title is required.");
        return store.UpdateParagraphAsync(id, title.Trim(), body ?? string.Empty, isArchived, formTemplateId, ct);
    }

    // ── Form templates (FR-08) ──
    public async Task<Guid> CreateFormTemplateAsync(string name, IReadOnlyCollection<NewFormField> fields, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Form template name is required.");
        return await store.CreateFormTemplateAsync(name.Trim(), fields, ct);
    }

    public async Task UpdateFormTemplateAsync(Guid id, string name, bool isActive, IReadOnlyCollection<NewFormField> fields, CancellationToken ct)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Form template name is required.");
        await store.UpdateFormTemplateAsync(id, name.Trim(), isActive, fields, ct);
    }
}
