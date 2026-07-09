using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record CreateCopyRequestCommand(
    Guid CourtId,
    Guid RoomId,
    DateOnly? CaseFilingDate,
    string CaseBaseNumber,
    CaseCategory Category,
    CaseUrgency Urgency,
    string? ExpediteRequestNumber,
    string? ReferenceNumber,
    Guid AssignedCopyistId,
    Guid? OriginalCopyId);
// Note: تاريخ الحجز (ReservationDate) is NOT a client input — the server assigns it at creation.

/// <summary>
/// FR-06: a Registry Head creates a request; the system allocates a sequential number and
/// routes it to the assigned copyist — all in one transaction (atomic create-with-number).
/// </summary>
public sealed class CreateCopyRequestService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    ICopyNumberAllocator allocator,
    IMiscNumberAllocator miscAllocator,
    IAuditWriter audit,
    IJcsQueries queries,
    IUnitOfWork unitOfWork)
{
    public async Task<CopyRequest> HandleAsync(CreateCopyRequestCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);   // BR-01

        // Resolve court/room/case-base. A متفرق (BR-11) is based on an Approved عادي copy and
        // INHERITS its court, room and رقم الأساس; it never carries its own رقم النسخة.
        Guid courtId = cmd.CourtId, roomId = cmd.RoomId;
        var caseBase = cmd.CaseBaseNumber;
        CopyRequest? original = null;

        if (cmd.Category == CaseCategory.Miscellaneous)
        {
            if (cmd.OriginalCopyId is not { } oid)
                throw new DomainException("القرار المتفرق يجب أن يستند إلى نسخة أصلية معتمدة.");
            original = await repository.GetAsync(oid, ct)
                       ?? throw new NotFoundException("النسخة الأصلية غير موجودة.");
            if (original.Category != CaseCategory.Normal)
                throw new DomainException("النسخة الأصلية يجب أن تكون قراراً عادياً.");
            if (original.State != CopyState.Approved)
                throw new DomainException("النسخة الأصلية يجب أن تكون معتمدة (مثبتة).");
            Guard.RequireAssignedCourt(currentUser, original.CourtId); // BR-06
            courtId = original.CourtId; roomId = original.RoomId; caseBase = original.CaseBaseNumber;
        }
        else
        {
            Guard.RequireAssignedCourt(currentUser, cmd.CourtId); // BR-06
            var room = await queries.GetRoomAsync(cmd.RoomId, ct)
                       ?? throw new NotFoundException("Room not found.");
            if (room.CourtId != cmd.CourtId || !room.IsActive)
                throw new DomainException("The selected room is not a valid active room of this court.");
            // رقم الأساس is unique per court for عادي copies (متفرق inherit the original's base).
            if (await repository.NormalCaseBaseExistsAsync(cmd.CourtId, cmd.CaseBaseNumber, ct))
                throw new DomainException("رقم الأساس مستخدم مسبقاً لقرار عادي في هذه المحكمة.");
        }

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var now = clock.UtcNow;
            // تاريخ الحجز is server-assigned (non-editable) — it also drives the numbering year.
            var reservationDate = DateOnly.FromDateTime(now.Date);

            var request = CopyRequest.Create(
                courtId, roomId, cmd.CaseFilingDate, caseBase, reservationDate,
                cmd.Category, cmd.Urgency, cmd.ExpediteRequestNumber, cmd.ReferenceNumber, original?.Id, currentUser.Id, now);

            if (cmd.Category == CaseCategory.Miscellaneous)
            {
                // متفرق: only رقم المتفرق (per the room's numbering policy; yearly). No رقم النسخة.
                var misc = await miscAllocator.AllocateAsync(courtId, roomId, reservationDate.Year, token);
                request.AssignMiscNumber(misc);
            }
            else
            {
                // عادي: allocate the sequential رقم النسخة inside the transaction (BR-07). Scope
                // (court-wide or per-room) follows the room's CopyNumberingPolicy (FR-03).
                var number = await allocator.AllocateAsync(courtId, roomId, reservationDate, token);
                request.AssignNumber(number);
            }

            // Created → InPreparation, routed to the copyist queue.
            request.AssignToCopyist(cmd.AssignedCopyistId, now);

            await repository.AddAsync(request, token);
            var copyJson = request.CopyNumber is null ? "null" : $"\"{request.CopyNumber}\"";
            var miscJson = request.MiscNumber?.ToString() ?? "null";
            var origJson = original is null ? "null" : $"\"{original.Id}\"";
            audit.Append(request.Id, AuditAction.Create,
                afterJson: $"{{\"copyNumber\":{copyJson},\"miscNumber\":{miscJson},\"originalCopyId\":{origJson},\"courtId\":\"{courtId}\"}}");

            await unitOfWork.SaveChangesAsync(token);
            return request;
        }, ct);
    }
}
