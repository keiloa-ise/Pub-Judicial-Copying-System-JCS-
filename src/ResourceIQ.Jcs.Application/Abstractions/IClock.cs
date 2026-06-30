namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Abstracts the system clock so workflow timing is testable and deterministic.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
