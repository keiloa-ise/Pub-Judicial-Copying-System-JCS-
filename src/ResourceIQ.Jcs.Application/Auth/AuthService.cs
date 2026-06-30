using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Application.Auth;

public sealed record LoginCommand(string Username, string Password);
public sealed record LoginResult(string Token, Guid UserId, string DisplayName, Role Role);

/// <summary>
/// FR-01: authenticates a user and issues a JWT. Failures return a single generic message —
/// we never reveal whether the username or the password was wrong, and never log either.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    public async Task<LoginResult> LoginAsync(LoginCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByUsernameAsync(cmd.Username.Trim(), ct);

        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, cmd.Password))
            throw new ForbiddenException("Invalid credentials.");

        var courtIds = await users.GetAssignedCourtIdsAsync(user.Id, ct);
        var token = tokenService.CreateToken(user, courtIds);
        return new LoginResult(token, user.Id, user.DisplayName, user.Role);
    }
}
