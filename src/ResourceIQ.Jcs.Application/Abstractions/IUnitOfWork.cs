namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>
/// Commits staged changes. <see cref="ExecuteInTransactionAsync"/> wraps a multi-step
/// workflow action (e.g. create-with-number, approve-and-lock, unlock-with-audit) so it
/// commits atomically or not at all (data-layer invariant 4).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
