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
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(AcceptCopyCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Copyist);

        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");
        if (request.AssignedCopyistId != currentUser.Id)
            throw new ForbiddenException("هذا القرار غير مُسنَد إليك.");

        // Acceptance must follow order: موقوف > مستعجل > عادي, and within a tier the OLDEST first.
        if (await repository.AnyUnacceptedRankedBeforeAsync(currentUser.Id, request.Urgency, request.CreatedUtc, ct))
            throw new DomainException("يجب قبول القرارات حسب الترتيب: الأعلى أولوية ثم الأقدم أولاً.");

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.AcceptByCopyist(currentUser.Id, clock.UtcNow);
            audit.Append(request.Id, AuditAction.Accept);
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }
}
