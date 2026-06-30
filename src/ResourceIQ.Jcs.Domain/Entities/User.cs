using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A system user. Passwords are stored only as a hash — never plaintext, never logged

/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;

    /// <summary>Hashed password. Set via the security layer; never assigned a plaintext value.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Courts this user is assigned to — drives BR-06 scoping.</summary>
    public ICollection<UserCourt> Courts { get; set; } = new List<UserCourt>();
}
