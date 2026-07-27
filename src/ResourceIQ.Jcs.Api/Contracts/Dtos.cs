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

// FR-06: escalate a non-approved copy to موقوف (Registry Head) — an optional note (رقم طلب التصعيد).
public sealed record SuspendRequest(string? Note);

public sealed record SaveDraftRequest(Guid? FormTemplateId, string FieldValuesJson, string SectionsJson, string DissentSectionsJson, string RebuttalSectionsJson, string Body);

// FR-15 print queue: the selected decisions to print (marked printed, rendered into one merged PDF).
public sealed record PrintManyRequest(IReadOnlyList<Guid> Ids);

public sealed record ReturnRequest(string Corrections);

public sealed record UnlockRequest(string Reason);
