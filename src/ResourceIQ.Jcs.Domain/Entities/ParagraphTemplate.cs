namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A reusable legal paragraph template (FR-09). Copyists may insert only non-archived
/// templates; archiving removes a template from the insertion list without deleting it.
/// </summary>
public class ParagraphTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The paragraph name shown when inserting (e.g. "طالب الانعدام"). Becomes the section heading.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Default legal text. Unicode (nvarchar). Never silently truncated or auto-corrected.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional form type this paragraph belongs to. When preparing a copy of that form type,
    /// only its paragraphs (plus global ones with null) are offered for insertion. null = global.
    /// </summary>
    public Guid? FormTemplateId { get; set; }
    public FormTemplate? FormTemplate { get; set; }

    public bool IsArchived { get; set; }
}
