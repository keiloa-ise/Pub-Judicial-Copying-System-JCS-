using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Infrastructure.Persistence;

namespace ResourceIQ.Jcs.Infrastructure.Audit;

/// <summary>
/// The single append path for audit history . It only ADDs entries to
/// the context; it never updates or deletes. Entries commit in the caller's transaction.
/// </summary>
public sealed class AuditWriter(JcsDbContext db, ICurrentUser currentUser, IClock clock) : IAuditWriter
{
    public void Append(
        Guid copyRequestId, AuditAction action,
        string? beforeJson = null, string? afterJson = null, string? reason = null)
    {
        Append(new AuditEntry
        {
            CopyRequestId = copyRequestId,
            ActorId = currentUser.Id,
            ActorName = currentUser.Name,
            Action = action,
            TimestampUtc = clock.UtcNow,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Reason = reason,
        });
    }

    public void Append(AuditEntry entry) => db.AuditEntries.Add(entry);
}
