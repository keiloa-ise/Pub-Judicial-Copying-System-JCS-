using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.Admin;

public sealed record UnlockCommand(Guid CopyRequestId, string Reason);

/// <summary>
/// FR-12: an Administrator unlocks an approved copy. The reason is MANDATORY and a matching
/// audit entry is written in the same transaction .
///
/// Decision #3 (RESOLVED): after unlock the assigned copyist re-edits the copy and re-submits
/// it to the reviewer for approval (Unlocked → UnderReview). This service performs only the
/// Approved → Unlocked step; the copyist drives the rest via the normal prepare/submit flow.
/// </summary>
public sealed class UnlockService(
    ICurrentUser currentUser,
    IClock clock,
    ICopyRequestRepository repository,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public Task HandleAsync(UnlockCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.Administrator); // BR-05

        if (string.IsNullOrWhiteSpace(cmd.Reason))
            throw new DomainException("Unlock requires a mandatory reason (FR-12).");

        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var request = await repository.GetAsync(cmd.CopyRequestId, token)
                          ?? throw new NotFoundException("Copy request not found.");

            request.Unlock(clock.UtcNow); // Approved → Unlocked
            audit.Append(request.Id, AuditAction.Unlock, reason: cmd.Reason.Trim());

            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, ct);
    }
}
