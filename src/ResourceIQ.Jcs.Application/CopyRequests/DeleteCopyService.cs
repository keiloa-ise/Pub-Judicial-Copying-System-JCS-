using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record DeleteCopyRequestCommand(Guid CopyRequestId);

/// <summary>
/// FR-16: the Registry Head deletes a last decision (via the deletion window), within their assigned
/// courts (BR-06). Two cases (BR-09/BR-11):
///   • متفرق — deletable when it is the LAST رقم المتفرق in its numbering scope; rolls back only the
///     رقم المتفرق counter (a متفرق has no رقم النسخة).
///   • عادي — deletable when it is the court+year latest رقم النسخة AND has NO linked متفرق copies
///     (else it would orphan them); rolls back the رقم النسخة counter.
/// On success the copy + content are removed and a <see cref="AuditAction.Delete"/> entry is appended
/// (audit is never deleted).
/// </summary>
public sealed class DeleteCopyService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    ICopyNumberAllocator copyNumbers,
    IMiscNumberAllocator miscNumbers,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task DeleteAsync(DeleteCopyRequestCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead); // FR-16

        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06 — within the head's courts
        if (request.AcceptedUtc is not null)
            throw new DomainException("لا يمكن الحذف: تم قبول هذا القرار من الناسخ.");

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var year = request.ReservationDate.Year;
            var copyJson = request.CopyNumber is null ? "null" : $"\"{request.CopyNumber}\"";
            var miscJson = request.MiscNumber is { } m ? m.ToString() : "null";

            void AppendDelete() => audit.Append(request.Id, AuditAction.Delete,
                beforeJson: $"{{\"copyNumber\":{copyJson},\"courtId\":\"{request.CourtId}\",\"category\":\"{request.Category}\",\"miscNumber\":{miscJson},\"state\":\"{request.State}\"}}");

            if (request.Category == CaseCategory.Miscellaneous)
            {
                // متفرق — must be the last رقم المتفرق in its scope; roll back the misc counter only.
                var lastMisc = await miscNumbers.PeekLastAsync(request.CourtId, request.RoomId, year, token);
                if (request.MiscNumber is null || lastMisc != request.MiscNumber)
                    throw new DomainException("لا يمكن الحذف: هذا ليس آخر قرار «متفرق» في هذا المستوى.");

                AppendDelete();
                repository.Remove(request); // CopyContent cascades; AuditEntries untouched
                await miscNumbers.ReleaseAsync(request.CourtId, request.RoomId, year, token);
            }
            else
            {
                // عادي — must not orphan any linked متفرق (BR-09)…
                if (await repository.AnyLinkedMiscAsync(request.Id, token))
                    throw new DomainException("لا يمكن الحذف: توجد قرارات متفرقة مرتبطة بهذه النسخة؛ احذفها أولاً.");
                // …and must be the court+year latest رقم النسخة.
                var seq = ParseSeq(request.CopyNumber);
                var lastCopy = await copyNumbers.PeekLastAsync(request.CourtId, year, token);
                if (seq is null || lastCopy != seq)
                    throw new DomainException("لا يمكن الحذف: هذا ليس آخر قرار في هذه المحكمة لنفس السنة.");

                AppendDelete();
                repository.Remove(request);
                await copyNumbers.ReleaseAsync(request.CourtId, year, token);
            }

            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }

    private static int? ParseSeq(string? copyNumber)
    {
        var parts = (copyNumber ?? "").Split('/');
        return parts.Length == 3 && int.TryParse(parts[2], out var s) ? s : null;
    }
}
