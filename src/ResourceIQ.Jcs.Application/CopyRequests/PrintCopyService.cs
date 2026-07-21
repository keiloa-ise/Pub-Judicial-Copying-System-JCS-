using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record PrintCopyCommand(Guid CopyRequestId);

/// <summary>
/// FR-15 print policy. Records a print of a copy and enforces the print ORDER (R1) ONLY for Approved
/// (مثبت) copies: the FIRST print of an approved copy is allowed only when NO higher-ranked approved
/// copy in the same court is still unprinted — same ranking as acceptance/approval (موقوف > مستعجل >
/// عادي, then oldest-first). Draft (غير مثبت) copies print unrestricted — any draft, in any order
/// (JC-31). Once a copy has been printed, it may be viewed and re-printed at any time (approved or
/// draft) — the first print clears it from the print-order queue.
/// The PDF itself is rendered by the API layer from the authoritative record after this succeeds.
/// </summary>
public sealed class PrintCopyService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(PrintCopyCommand cmd, CancellationToken ct)
    {
        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");

        // Same court-scoped visibility as viewing (BR-06); Administrators are unrestricted.
        if (currentUser.Role != Role.Administrator)
            Guard.RequireAssignedCourt(currentUser, request.CourtId);

        var isApproved = request.State == CopyState.Approved;

        // R1 (JC-31): the FIRST print of an APPROVED copy must follow the queue order. Draft (غير
        // مثبت) copies are exempt — printing is unrestricted until the copy is approved. Once printed,
        // a copy — approved or draft — may be viewed and re-printed at any time (it has already left
        // the print-order queue).
        var firstPrint = request.PrintedUtc is null;
        if (isApproved && firstPrint && await repository.AnyUnprintedRankedBeforeAsync(
                [request.CourtId], isApproved, request.Urgency, request.CreatedUtc, ct))
            throw new DomainException("يجب طباعة القرارات حسب الأولوية والتسلسل: الأعلى أولوية ثم الأقدم أولاً.");

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.MarkPrinted(currentUser.Id, clock.UtcNow);
            audit.Append(request.Id, AuditAction.Print);
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }
}
