namespace ResourceIQ.Jcs.Infrastructure.Security;

/// <summary>JWT settings, bound from configuration ("Jwt" section). The signing key is a
/// secret — supply it via environment/secret store, never commit a real key.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "jcs";
    public string Audience { get; set; } = "jcs";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
