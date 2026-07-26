namespace ResourceIQ.Jcs.Api.Bootstrap;

public sealed class FormDraftCleanupOptions
{
    public const string SectionName = "FormDraftCleanup";

    public bool Enabled { get; set; } = true;
    public int OlderThanDays { get; set; } = 30;
    public string Cron { get; set; } = "0 3 * * *";
    public string TimeZoneId { get; set; } = "UTC";
}
