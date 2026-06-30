using ResourceIQ.Jcs.Application.Abstractions;

namespace ResourceIQ.Jcs.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
