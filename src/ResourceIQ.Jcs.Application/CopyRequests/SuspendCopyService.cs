using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record SuspendCopyCommand(Guid CopyRequestId);

/// <summary>
/// Registry Head escalates a non-approved copy to موقوف, reusing the same role/court
/// protections as expedite while keeping the audit action explicit.
/// </summary>
public sealed class SuspendCopyService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(SuspendCopyCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);

        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");
        Guard.RequireAssignedCourt(currentUser, request.CourtId);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.EscalateToSuspended(clock.UtcNow);
            audit.Append(request.Id, AuditAction.Suspend,
                afterJson: "{\"urgency\":\"Suspended\"}");
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }
}
