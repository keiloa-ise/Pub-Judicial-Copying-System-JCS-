namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// Allocates the sequential copy number (BR-07) server-side and atomically, inside the
/// create-request transaction.
///
/// PRD decision #1 — the رقم النسخة scope is per **room policy** (court-wide or per-room, FR-03) and
/// resets yearly. This seam takes the court, the room, and the reservation date; the concrete
/// allocator reads the room's <c>CopyNumberingPolicy</c> to pick the counter scope and the printed
/// format. The default DI registration intentionally throws until a concrete allocator is chosen.
/// </summary>
public interface ICopyNumberAllocator
{
    /// <summary>Allocates رقم النسخة for a copy in the given room. The numbering scope (court-wide or
    /// per-room) and the printed format follow the room's <c>CopyNumberingPolicy</c> (FR-03).</summary>
    Task<string> AllocateAsync(Guid courtId, Guid roomId, DateOnly reservationDate, CancellationToken ct);

    /// <summary>FR-16: roll back the room's scope+year counter by one when the latest copy is deleted,
    /// so the next allocation reuses the freed number (no gap). The caller guarantees the deleted
    /// copy holds that scope+year's last number.</summary>
    Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct);

    /// <summary>Current last-issued number for the room's scope+year (null if none) — used to verify a
    /// copy is the scope+year latest before deleting (FR-16, no-gap guard).</summary>
    Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct);
}

/// <summary>
/// Allocates رقم المتفرق — the extra sequential number on متفرق copies. Scope is per-room for
/// جزائية (criminal) courts and per-court otherwise, reset yearly. Atomic, inside the create
/// transaction; <see cref="ReleaseAsync"/> rolls it back when the latest copy is deleted (FR-16).
/// </summary>
public interface IMiscNumberAllocator
{
    Task<int> AllocateAsync(Guid courtId, Guid roomId, int year, CancellationToken ct);
    Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct);

    /// <summary>Current last-issued رقم المتفرق for the room's scope+year (null if none) — used to
    /// verify a copy is the last متفرق in its scope before deleting (FR-16, no-gap guard).</summary>
    Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct);
}
