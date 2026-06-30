namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// An admin-defined title/role (صفة) a judging-panel member can hold — e.g. رئيس الهيئة,
/// نائب الرئيس, عضو, مستشار. The copyist picks one of these (active) titles per panel member
/// while editing a copy; the chosen title text is stored on the copy content and printed verbatim.
/// Names are unique. <see cref="DisplayOrder"/> controls the order shown in the pickers.
/// </summary>
public class PanelMemberTitle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
