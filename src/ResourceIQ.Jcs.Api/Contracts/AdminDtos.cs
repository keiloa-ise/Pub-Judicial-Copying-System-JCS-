using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Api.Contracts;

public sealed record CreateCourtRequest(string Code, string Name);
public sealed record UpdateCourtRequest(string Name, bool IsActive);

public sealed record CreateRoomRequest(Guid CourtId, string Code, string Name, NumberingPolicy NumberingPolicy, string? NumberingLevel);
public sealed record UpdateRoomRequest(string Name, bool IsActive, NumberingPolicy NumberingPolicy, string? NumberingLevel);

// Numbering start points (FR-17). LastNumber = last issued number; auto numbering continues at +1.
public sealed record SetCopyNumberStartRequest(Guid CourtId, int Year, int LastNumber);
public sealed record SetMiscNumberStartRequest(Guid CourtId, NumberingPolicy Scope, Guid? RoomId, string? Level, int Year, int LastNumber);

public sealed record CreateUserRequest(
    string Username, string DisplayName, Role Role, string Password, Guid[] CourtIds);
public sealed record UpdateUserRequest(string DisplayName, Role Role);
public sealed record SetActiveRequest(bool IsActive);
public sealed record SetCourtsRequest(Guid[] CourtIds);
public sealed record ResetPasswordRequest(string Password);

public sealed record CreateJudgeRequest(string Name, Guid[] RoomIds);
public sealed record UpdateJudgeRequest(string Name, bool IsActive, Guid[] RoomIds);

public sealed record CreatePanelTitleRequest(string Name, int DisplayOrder);
public sealed record UpdatePanelTitleRequest(string Name, bool IsActive, int DisplayOrder);

public sealed record CreateParagraphRequest(string Title, string Body, Guid? FormTemplateId);
public sealed record UpdateParagraphRequest(string Title, string Body, bool IsArchived, Guid? FormTemplateId);

public sealed record CreateFormTemplateRequest(string Name, NewFormField[] Fields);
public sealed record UpdateFormTemplateRequest(string Name, bool IsActive, NewFormField[] Fields);
