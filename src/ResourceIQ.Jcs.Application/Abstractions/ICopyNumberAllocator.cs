namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// Allocates the sequential copy number (BR-07) server-side and atomically, inside the
/// create-request transaction.
///
/// ⚠ [OPEN] PRD decision #1 — the uniqueness SCOPE (global vs per-court vs per-court-per-year)
/// and the number FORMAT are undefined. This seam takes both <paramref name="courtId"/> and
/// <paramref name="reservationDate"/> so any chosen scope is implementable without changing
/// the signature. Infrastructure ships concrete allocators for each scope; the default DI
/// registration intentionally throws until the scope is confirmed — do NOT pick one here.
/// </summary>
public interface ICopyNumberAllocator
{
    Task<string> AllocateAsync(Guid courtId, DateOnly reservationDate, CancellationToken ct);

    /// <summary>FR-16: roll back the court+year counter by one when the latest copy is deleted,
    /// so the next allocation reuses the freed number (no gap). The caller guarantees the deleted
    /// copy holds that court+year's last number.</summary>
    Task ReleaseAsync(Guid courtId, int year, CancellationToken ct);

    /// <summary>Current last-issued number for the court+year (null if none) — used to verify a copy
    /// is the court+year latest before deleting (FR-16, no-gap guard).</summary>
    Task<int?> PeekLastAsync(Guid courtId, int year, CancellationToken ct);
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
