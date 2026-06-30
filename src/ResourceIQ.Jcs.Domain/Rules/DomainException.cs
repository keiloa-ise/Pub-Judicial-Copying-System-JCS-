namespace ResourceIQ.Jcs.Domain.Rules;

/// <summary>
/// Raised when a domain invariant or business rule (BR-*) would be violated.
/// The API translates this to a 409/422 — never a 500.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
