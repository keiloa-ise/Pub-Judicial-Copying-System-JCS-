using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record ExpediteCopyCommand(Guid CopyRequestId, string ExpediteRequestNumber);

/// <summary>
/// FR-06: the Registry Head escalates a NON-approved copy to مستعجل at any time, supplying the
/// expedite-request number. This raises the copy's work-queue priority (BR-10). Audited as Expedite.
/// </summary>
public sealed class ExpediteCopyService(
    ICurrentUser currentUser,
    ICopyRequestRepository repository,
    IClock clock,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(ExpediteCopyCommand cmd, CancellationToken ct)
    {
        Guard.RequireRole(currentUser, Role.RegistryHead);

        var request = await repository.GetAsync(cmd.CopyRequestId, ct)
                      ?? throw new NotFoundException("Copy request not found.");
        Guard.RequireAssignedCourt(currentUser, request.CourtId); // BR-06

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.EscalateToExpedited(cmd.ExpediteRequestNumber, clock.UtcNow); // validates non-approved + number
            audit.Append(request.Id, AuditAction.Expedite,
                afterJson: $"{{\"expediteRequestNumber\":\"{request.ExpediteRequestNumber}\"}}");
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }
}
