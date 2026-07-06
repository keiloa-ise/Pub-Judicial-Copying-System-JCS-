namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// The editable content of a copy request: dynamic form field values plus the legal body
/// text (FR-07). One-to-one with <see cref="CopyRequest"/>.
/// </summary>
public class CopyContent
{
    // No Guid.NewGuid() initializer on purpose: CopyContent is persisted only via the
    // CopyRequest navigation graph (never db.Add directly). A pre-set key would make EF's
    // add-vs-update heuristic emit an UPDATE for a brand-new row. Letting the key default to
    // Guid.Empty makes EF treat it as Added and generate the key on insert.
    public Guid Id { get; set; }

    public Guid CopyRequestId { get; set; }
    public CopyRequest? CopyRequest { get; set; }

    public Guid? FormTemplateId { get; set; }
    public FormTemplate? FormTemplate { get; set; }

    /// <summary>Fixed field values keyed by <see cref="FormField.Key"/>, serialized as JSON.</summary>
    public string FieldValuesJson { get; set; } = "{}";

    /// <summary>
    /// Ordered, editable sections inserted from paragraph templates — JSON array of
    /// { "title", "text" }. This is the dynamic body of the copy (everything except the fixed
    /// header fields). Legal text stored verbatim.
    /// </summary>
    public string SectionsJson { get; set; } = "[]";

    /// <summary>
    /// Dissent appendix (الرأي المخالف): ordered sections in the same shape as
    /// <see cref="SectionsJson"/> (JSON array of { "title", "text" }). When one or more judges
    /// dissent, this holds the reason text; it is printed on a NEW page after the decision and
    /// signed by the dissenting judges. "[]" when there is no dissent. Which judges dissent is
    /// stored inside <see cref="FieldValuesJson"/> (members[].dissenting + presidentDissenting).
    /// </summary>
    public string DissentSectionsJson { get; set; } = "[]";

    /// <summary>Legacy free-text body (superseded by <see cref="SectionsJson"/>). Kept for compatibility.</summary>
    public string Body { get; set; } = string.Empty;
}
