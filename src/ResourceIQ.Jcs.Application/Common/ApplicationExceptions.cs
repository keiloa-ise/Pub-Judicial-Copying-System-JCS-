namespace ResourceIQ.Jcs.Application.Common;

/// <summary>Caller is authenticated but not permitted to perform the action (→ HTTP 403).</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>The requested resource does not exist (→ HTTP 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
