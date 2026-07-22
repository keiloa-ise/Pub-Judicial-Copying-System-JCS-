using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

public sealed class FormDraftStore(JcsDbContext db) : IFormDraftStore
{
    public Task<FormDraft?> GetAsync(Guid userId, string formKey, CancellationToken ct) =>
        db.FormDrafts.FirstOrDefaultAsync(x => x.UserId == userId && x.FormKey == formKey, ct);

    public async Task AddAsync(FormDraft draft, CancellationToken ct) =>
        await db.FormDrafts.AddAsync(draft, ct);

    public void Remove(FormDraft draft) => db.FormDrafts.Remove(draft);

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) =>
        db.FormDrafts.Where(x => x.UpdatedUtc < cutoffUtc).ExecuteDeleteAsync(ct);
}
