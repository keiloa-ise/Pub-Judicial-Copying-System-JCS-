using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// The ONLY path that writes audit history. It appends; it never updates or deletes
/// The entry is staged into the current unit of work and committed
/// in the same transaction as the action it records.
/// </summary>
public interface IAuditWriter
{
    /// <summary>Stage an audit entry for the current actor. Caller commits via the unit of work.</summary>
    void Append(
        Guid copyRequestId,
        AuditAction action,
        string? beforeJson = null,
        string? afterJson = null,
        string? reason = null);

    /// <summary>Stage a fully-built entry (used when the actor is not the ambient current user).</summary>
    void Append(AuditEntry entry);
}
