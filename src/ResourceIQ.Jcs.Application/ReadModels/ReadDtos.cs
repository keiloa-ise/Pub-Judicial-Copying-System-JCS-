using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.ReadModels;

// Read-side projections returned to the API. Separate from domain entities so reads never
// expose mutable aggregates.

public sealed record CopyRequestListItem(
    Guid Id,
    string? CopyNumber,
    CopyState State,
    Guid CourtId,
    string CourtName,
    Guid RoomId,
    string RoomName,
    string CaseBaseNumber,
    DateOnly? CaseFilingDate,
    DateOnly ReservationDate,
    CaseCategory Category,
    CaseUrgency Urgency,
    string? ExpediteRequestNumber,
    int? MiscNumber,
    Guid? AssignedCopyistId,
    string? AssignedCopyistName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? AcceptedUtc);

public sealed record CopyRequestDetail(
    Guid Id,
    string? CopyNumber,
    CopyState State,
    Guid CourtId,
    string CourtName,
    Guid RoomId,
    string RoomName,
    string CaseBaseNumber,
    DateOnly? CaseFilingDate,
    DateOnly ReservationDate,
    CaseCategory Category,
    CaseUrgency Urgency,
    string? ExpediteRequestNumber,
    string? ReferenceNumber,
    int? MiscNumber,
    Guid? AssignedCopyistId,
    string? AssignedCopyistName,
    Guid? FormTemplateId,
    string FieldValuesJson,
    string SectionsJson,
    string DissentSectionsJson,
    string Body,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ApprovedUtc,
    DateTimeOffset? AcceptedUtc,
    Guid? OriginalCopyId,
    string? OriginalCopyNumber,
    IReadOnlyList<LinkedMiscDto> LinkedMisc);

/// <summary>BR-11: a متفرق copy linked to an original عادي copy (shown under the original's detail).</summary>
public sealed record LinkedMiscDto(
    Guid Id, int? MiscNumber, string? ReferenceNumber, CopyState State, DateOnly ReservationDate);

/// <summary>BR-11: an Approved عادي copy a Registry Head may base a new متفرق on (the original picker).</summary>
public sealed record OriginalCopyOption(
    Guid Id, string CopyNumber, Guid CourtId, string CourtName, string CaseBaseNumber, DateOnly ReservationDate);

/// <summary>FR-16: the latest عادي copy in a court (court+year). Deletable only when it has no linked
/// متفرق copies (HasLinkedMisc = false), so deleting it never orphans a متفرق (BR-09).</summary>
public sealed record DeletableCopyDto(
    Guid CourtId,
    string CourtName,
    Guid CopyRequestId,
    string CopyNumber,
    string RoomName,
    CopyState State,
    bool HasLinkedMisc);

/// <summary>FR-16: the last متفرق in a numbering scope (scope+year) — deletable by its numbering
/// scope (BR-11). Rolling it back touches only the رقم المتفرق counter.</summary>
public sealed record DeletableMiscDto(
    string ScopeKey,
    Guid CourtId,
    string CourtName,
    string ScopeLabel,
    Guid CopyRequestId,
    int MiscNumber,
    string? OriginalCopyNumber,
    string? ReferenceNumber,
    CopyState State);

/// <summary>FR-16: both deletion sections shown to the Registry Head — عادي per court, متفرق per scope.</summary>
public sealed record DeletionTargetsDto(
    IReadOnlyList<DeletableCopyDto> Normals,
    IReadOnlyList<DeletableMiscDto> Miscs);

public sealed record AuditEntryDto(
    string ActorName,
    AuditAction Action,
    DateTimeOffset TimestampUtc,
    string? Reason,
    string? BeforeJson,
    string? AfterJson);

public sealed record LookupItem(Guid Id, string Name);

public sealed record CourtDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record RoomDto(Guid Id, Guid CourtId, string Code, string Name, bool IsActive,
    NumberingPolicy NumberingPolicy, string? NumberingLevel, CopyNumberingPolicy CopyNumberingPolicy);

/// <summary>FR-17: a رقم النسخة start-point counter — last number issued for a court in a year.</summary>
public sealed record CopyNumberCounterDto(Guid CourtId, string CourtCode, string CourtName, Guid? RoomId, string ScopeLabel, int Year, int LastNumber);

/// <summary>FR-17: a رقم المتفرق start-point counter — last number issued for a numbering scope in a year.</summary>
public sealed record MiscNumberCounterDto(string ScopeKey, Guid CourtId, string CourtName, string ScopeLabel, int Year, int LastNumber);

public sealed record JudgeDto(Guid Id, string Name, bool IsActive, IReadOnlyList<Guid> RoomIds);

/// <summary>Admin view of a panel-member title (صفة) — e.g. رئيس الهيئة, عضو, مستشار (FR-04 sibling).</summary>
public sealed record PanelMemberTitleDto(Guid Id, string Name, bool IsActive, int DisplayOrder);

public sealed record UserDto(
    Guid Id, string Username, string DisplayName, Role Role, bool IsActive, IReadOnlyList<Guid> CourtIds);

public sealed record ParagraphTemplateDto(Guid Id, string Title, string Body, bool IsArchived, Guid? FormTemplateId);

public sealed record FormFieldDto(Guid Id, string Key, string Label, string Type, string? ValidationRulesJson, int Order);

public sealed record FormTemplateDto(Guid Id, string Name, bool IsActive, IReadOnlyList<FormFieldDto> Fields);

/// <summary>Filter passed to the query store. The read SERVICE fills the SCOPE parts
/// (AssignedCopyistId / CreatedById / CourtIds) from the caller's role + court assignments;
/// the SEARCH parts come from the user's advanced-search inputs. Controllers never set scope.</summary>
public sealed record CopyRequestFilter(
    IReadOnlyCollection<CopyState>? States = null,
    Guid? AssignedCopyistId = null,
    Guid? CreatedById = null,
    IReadOnlyCollection<Guid>? CourtIds = null,
    string? CopyNumber = null,
    string? CaseBaseNumber = null,
    DateOnly? FromReservation = null,
    DateOnly? ToReservation = null);

/// <summary>Advanced-search inputs supplied by the user (narrow within their allowed scope).</summary>
public sealed record CopyRequestSearch(
    CopyState? State = null,
    string? CopyNumber = null,
    string? CaseBaseNumber = null,
    Guid? CourtId = null,
    DateOnly? FromReservation = null,
    DateOnly? ToReservation = null);
