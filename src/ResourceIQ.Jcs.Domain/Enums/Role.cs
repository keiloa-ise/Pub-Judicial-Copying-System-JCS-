namespace ResourceIQ.Jcs.Domain.Enums;

/// <summary>
/// System roles. Names map to the Arabic UI labels.
/// RegistryHead = رئيس الديوان, Copyist = الناسخ, Reviewer = المدقق.
/// Authorization is always enforced server-side against these.
/// </summary>
public enum Role
{
    Administrator = 1,
    RegistryHead = 2,
    Copyist = 3,
    Reviewer = 4,
}
