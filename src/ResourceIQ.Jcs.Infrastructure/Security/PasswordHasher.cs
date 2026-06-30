using Microsoft.AspNetCore.Identity;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Security;

/// <summary>
/// Wraps ASP.NET Core Identity's PBKDF2-based <see cref="PasswordHasher{TUser}"/>. Only the
/// hash is ever persisted; plaintext is never stored or logged .
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(new User(), password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(new User(), hash, password) != PasswordVerificationResult.Failed;
}
