using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>JC-32: persistence for recoverable form drafts (one row per user + form key).</summary>
public interface IFormDraftStore
{
    Task<FormDraft?> GetAsync(Guid userId, string formKey, CancellationToken ct);
    Task AddAsync(FormDraft draft, CancellationToken ct);
    void Remove(FormDraft draft);
    /// <summary>Cleanup: delete all drafts not updated since the cutoff. Returns the number removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct);
}
