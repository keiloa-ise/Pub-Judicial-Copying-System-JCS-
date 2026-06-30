using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Abstractions;

public sealed record NewFormField(string Key, string Label, string Type, string? ValidationRulesJson, int Order);

/// <summary>
/// Administrator-managed configuration data (FR-02…FR-04, FR-08, FR-09). Each method persists
/// atomically. Authorization (Administrator) is enforced by the AdminService, not here.
/// </summary>
public interface IAdminStore
{
    // Courts (FR-03)
    Task<bool> CourtCodeExistsAsync(string code, CancellationToken ct);
    Task<Guid> CreateCourtAsync(string code, string name, CancellationToken ct);
    Task UpdateCourtAsync(Guid id, string name, bool isActive, CancellationToken ct);

    // Rooms (غرف) — a court is a set of rooms; code is unique within the court.
    Task<bool> CourtExistsAsync(Guid courtId, CancellationToken ct);
    Task<bool> RoomCodeExistsInCourtAsync(Guid courtId, string code, CancellationToken ct);
    Task<Guid> CreateRoomAsync(Guid courtId, string code, string name, NumberingPolicy policy, string? level, CancellationToken ct);
    Task UpdateRoomAsync(Guid id, string name, bool isActive, NumberingPolicy policy, string? level, CancellationToken ct);

    // Numbering start points (FR-17) — set "last issued number" so auto numbering continues at +1.
    // Guards against lowering below the highest number already used in the system for that scope.
    Task SetCopyNumberStartAsync(Guid courtId, int year, int lastNumber, CancellationToken ct);
    Task SetMiscNumberStartAsync(Guid courtId, string scopeKey, int year, int lastNumber, CancellationToken ct);

    // Users (FR-02)
    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);
    Task<Guid> CreateUserAsync(string username, string displayName, Role role, string passwordHash,
        IReadOnlyCollection<Guid> courtIds, CancellationToken ct);
    Task UpdateUserAsync(Guid id, string displayName, Role role, CancellationToken ct);
    Task SetUserActiveAsync(Guid id, bool active, CancellationToken ct);
    Task SetUserCourtsAsync(Guid id, IReadOnlyCollection<Guid> courtIds, CancellationToken ct);
    Task SetPasswordHashAsync(Guid id, string passwordHash, CancellationToken ct);

    // Judges (FR-04) — a judge is assigned to one or more rooms (غرف).
    Task<IReadOnlyList<JudgeDto>> ListJudgesAsync(CancellationToken ct);
    Task<bool> RoomsExistAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct);
    Task<Guid> CreateJudgeAsync(string name, IReadOnlyCollection<Guid> roomIds, CancellationToken ct);
    Task UpdateJudgeAsync(Guid id, string name, bool isActive, IReadOnlyCollection<Guid> roomIds, CancellationToken ct);

    // Panel-member titles (صفات) — admin-defined list a copyist picks from per panel member.
    Task<IReadOnlyList<PanelMemberTitleDto>> ListPanelMemberTitlesAsync(CancellationToken ct);
    Task<Guid> CreatePanelMemberTitleAsync(string name, int displayOrder, CancellationToken ct);
    Task UpdatePanelMemberTitleAsync(Guid id, string name, bool isActive, int displayOrder, CancellationToken ct);

    // Paragraph templates (FR-09) — optionally scoped to a form type.
    Task<Guid> CreateParagraphAsync(string title, string body, Guid? formTemplateId, CancellationToken ct);
    Task UpdateParagraphAsync(Guid id, string title, string body, bool isArchived, Guid? formTemplateId, CancellationToken ct);

    // Form templates (FR-08)
    Task<Guid> CreateFormTemplateAsync(string name, IReadOnlyCollection<NewFormField> fields, CancellationToken ct);
    Task UpdateFormTemplateAsync(Guid id, string name, bool isActive, IReadOnlyCollection<NewFormField> fields, CancellationToken ct);
}
