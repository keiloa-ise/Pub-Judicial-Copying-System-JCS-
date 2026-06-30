using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>
/// Commits via the <see cref="JcsDbContext"/>. <see cref="ExecuteInTransactionAsync"/> opens
/// a real DB transaction so copy-number allocation and the state change commit atomically
/// Concurrency-sensitive paths run under Serializable to prevent
/// duplicate numbers and lost-update on the approval lock.
/// </summary>
public sealed class UnitOfWork(JcsDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var result = await action(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
