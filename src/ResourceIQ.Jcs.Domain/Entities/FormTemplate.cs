namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A dynamic form template (FR-08). Copy content forms render from a template's fields —
/// the field list is never hardcoded in the UI.
/// </summary>
public class FormTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<FormField> Fields { get; set; } = new List<FormField>();
}
