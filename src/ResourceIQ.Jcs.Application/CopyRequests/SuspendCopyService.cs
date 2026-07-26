using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.CopyRequests;

public sealed record SuspendCopyCommand(Guid CopyRequestId, string? Note = null);

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

        var note = string.IsNullOrWhiteSpace(cmd.Note) ? null : cmd.Note.Trim();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            request.EscalateToSuspended(note, clock.UtcNow);
            audit.Append(request.Id, AuditAction.Suspend, reason: note,
                afterJson: "{\"urgency\":\"Suspended\"}");
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, ct);
    }
}
