using ResourceIQ.Jcs.Application.FormDrafts;

namespace ResourceIQ.Jcs.Api.Bootstrap;

/// <summary>
/// JC-32: periodically deletes stale form drafts. A lightweight hosted service (PeriodicTimer) — no
/// external job framework or extra DB schema. Config (all optional):
///   FormDraftCleanup:OlderThanDays (default 30), FormDraftCleanup:IntervalHours (default 24),
///   FormDraftCleanup:Enabled (default true).
/// </summary>
public sealed class FormDraftCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<FormDraftCleanupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!(config.GetValue<bool?>("FormDraftCleanup:Enabled") ?? true))
            return;

        var olderThanDays = Math.Max(1, config.GetValue<int?>("FormDraftCleanup:OlderThanDays") ?? 30);
        var intervalHours = Math.Max(1, config.GetValue<int?>("FormDraftCleanup:IntervalHours") ?? 24);

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } // let startup settle
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<FormDraftCleanupService>();
                var deleted = await cleanup.DeleteOlderThanAsync(olderThanDays, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} stale form drafts (older than {Days} days).", deleted, olderThanDays);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Form-draft cleanup failed; will retry next interval."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
