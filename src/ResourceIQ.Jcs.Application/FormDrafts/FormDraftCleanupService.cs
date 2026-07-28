using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.FormDrafts;

/// <summary>JC-32: deletes stale drafts (no user context) — invoked by the scheduled cleanup service.</summary>
public sealed class FormDraftCleanupService(IClock clock, IFormDraftStore drafts)
{
    public Task<int> DeleteOlderThanAsync(int olderThanDays, CancellationToken ct)
    {
        if (olderThanDays < 1) throw new DomainException("olderThanDays must be at least 1.");
        return drafts.DeleteOlderThanAsync(clock.UtcNow.AddDays(-olderThanDays), ct);
    }
}
