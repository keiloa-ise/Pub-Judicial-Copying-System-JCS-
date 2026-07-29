using System.Text.Json;
using System.Text.Json.Nodes;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record AcceptCopyCommand(Guid CopyRequestId);

/// <summary>
/// FR-07: the assigned Copyist accepts a copy before editing it. Acceptance is recorded
/// (time + actor) and is enforced in **priority order** (BR-10): a copyist cannot accept a copy
/// while a higher-priority copy of theirs is still unaccepted.
/// </summary>
public sealed class AcceptCopyService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    IJcsQueries queries,
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>FieldValues key for the fixed الهيئة الحاكمة text field (see DbSeeder AddFixedFields).</summary>
    private const string ChamberFieldKey = "chamber";

    public async Task HandleAsync(AcceptCopyCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Copyist);

        var request = await repository.GetWithContentAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");
        if (request.AssignedCopyistId != currentUser.Id)
            throw new ForbiddenException("هذا القرار غير مُسنَد إليك.");

        // Acceptance must follow order: موقوف > مستعجل > عادي, and within a tier the OLDEST first.
        if (await repository.AnyUnacceptedRankedBeforeAsync(currentUser.Id, request.Urgency, request.CreatedUtc, ct))
            throw new DomainException("يجب قبول القرارات حسب الترتيب: الأعلى أولوية ثم الأقدم أولاً.");

        // One-time pre-fill of الهيئة الحاكمة with the copy's المحكمة/الغرفة (editable afterwards).
        var seededValues = await BuildSeededFieldValuesAsync(request, ct);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.AcceptByCopyist(currentUser.Id, clock.UtcNow);
            if (seededValues is not null)
                request.SeedFieldValuesOnAccept(seededValues, clock.UtcNow);
            audit.Append(request.Id, AuditAction.Accept);
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }

    /// <summary>Returns the FieldValues JSON to seed at acceptance — the existing values with الهيئة الحاكمة
    /// set to "المحكمة - الغرفة", but ONLY when that field is still empty (never overwrites). Returns null
    /// when there is nothing to seed (already filled, or names unavailable) so acceptance leaves content untouched.</summary>
    private async Task<string?> BuildSeededFieldValuesAsync(Domain.Entities.CopyRequest request, CancellationToken ct)
    {
        var existing = JsonNode.Parse(
            string.IsNullOrWhiteSpace(request.Content?.FieldValuesJson) ? "{}" : request.Content!.FieldValuesJson)
            as JsonObject ?? new JsonObject();

        if (!string.IsNullOrWhiteSpace(existing[ChamberFieldKey]?.ToString()))
            return null; // already set — respect the copyist's value, seed once only

        var names = await queries.GetCourtAndRoomNamesAsync(request.CourtId, request.RoomId, ct);
        if (names is null) return null;

        existing[ChamberFieldKey] = $"{names.CourtName} - {names.RoomName}";
        return existing.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
