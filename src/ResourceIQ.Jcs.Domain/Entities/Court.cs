namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>A court (FR-03). Code is unique; name is required.</summary>
public class Court
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
