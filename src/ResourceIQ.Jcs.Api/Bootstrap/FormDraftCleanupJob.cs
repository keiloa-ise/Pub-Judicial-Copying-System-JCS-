using Microsoft.Extensions.Options;
using ResourceIQ.Jcs.Application.FormDrafts;

namespace ResourceIQ.Jcs.Api.Bootstrap;

public sealed class FormDraftCleanupJob(
    FormDraftCleanupService cleanup,
    IOptions<FormDraftCleanupOptions> options,
    ILogger<FormDraftCleanupJob> logger)
{
    public const string RecurringJobId = "form-drafts-cleanup";

    public async Task DeleteOlderThanConfiguredAsync()
    {
        var olderThanDays = options.Value.OlderThanDays;
        var deleted = await cleanup.DeleteOlderThanAsync(olderThanDays, CancellationToken.None);
        logger.LogInformation("Deleted {DeletedCount} stale form drafts older than {OlderThanDays} days.",
            deleted, olderThanDays);
    }
}
