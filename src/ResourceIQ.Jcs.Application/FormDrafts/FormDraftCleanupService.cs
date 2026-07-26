using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Application.FormDrafts;

public sealed class FormDraftCleanupService(IClock clock, IFormDraftStore drafts)
{
    public async Task<int> DeleteOlderThanAsync(int olderThanDays, CancellationToken ct)
    {
        if (olderThanDays < 1) throw new DomainException("olderThanDays must be at least 1.");

        var cutoff = clock.UtcNow.AddDays(-olderThanDays);
        return await drafts.DeleteOlderThanAsync(cutoff, ct);
    }
}
