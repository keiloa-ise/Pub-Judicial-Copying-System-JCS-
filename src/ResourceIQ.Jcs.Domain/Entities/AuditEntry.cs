using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
///
/// This entity is NEVER updated or deleted through any code path. It is created only via
/// the audit writer and exposed through no mutable repository method. Properties are
/// init-only to make in-memory mutation a compile error as a first line of defense.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CopyRequestId { get; init; }

    /// <summary>Acting user id.</summary>
    public Guid ActorId { get; init; }
    public string ActorName { get; init; } = string.Empty;

    public AuditAction Action { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Before/after snapshots where the action changes content; null otherwise.</summary>
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }

    /// <summary>Mandatory free-text reason for actions that require one (e.g. unlock, FR-12).</summary>
    public string? Reason { get; init; }
}
