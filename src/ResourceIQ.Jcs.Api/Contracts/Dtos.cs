using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Api.Contracts;

// Request DTOs — validated at the API boundary; domain rules stay in the service layer.

public sealed record LoginRequest(string Username, string Password);

public sealed record CreateCopyRequestRequest(
    Guid CourtId, Guid RoomId, DateOnly? CaseFilingDate, string CaseBaseNumber,
    CaseCategory Category, CaseUrgency Urgency, string? ExpediteRequestNumber, string? ReferenceNumber,
    Guid AssignedCopyistId, Guid? OriginalCopyId);

// FR-06: escalate a non-approved copy to مستعجل (Registry Head) — expedite number required.
public sealed record ExpediteRequest(string ExpediteRequestNumber);

public sealed record SaveDraftRequest(Guid? FormTemplateId, string FieldValuesJson, string SectionsJson, string Body);

public sealed record ReturnRequest(string Corrections);

public sealed record UnlockRequest(string Reason);
