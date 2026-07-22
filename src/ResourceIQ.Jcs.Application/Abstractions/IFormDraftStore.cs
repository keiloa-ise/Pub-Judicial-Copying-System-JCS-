using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

public interface IFormDraftStore
{
    Task<FormDraft?> GetAsync(Guid userId, string formKey, CancellationToken ct);
    Task AddAsync(FormDraft draft, CancellationToken ct);
    void Remove(FormDraft draft);
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct);
}
