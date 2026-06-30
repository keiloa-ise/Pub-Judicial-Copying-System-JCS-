namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A field definition within a <see cref="FormTemplate"/> (FR-08). Validation rules are
/// stored as JSON and applied dynamically by the renderer and the API boundary validator.
/// </summary>
public class FormField
{
    // No Guid.NewGuid() initializer: FormFields are persisted via the FormTemplate navigation
    // graph (incl. re-syncing an existing template). A pre-set key makes EF's add-vs-update
    // heuristic emit an UPDATE for a brand-new row; a default key makes it INSERT correctly.
    public Guid Id { get; set; }
    public Guid FormTemplateId { get; set; }
    public FormTemplate? FormTemplate { get; set; }

    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";

    /// <summary>JSON validation rules (required, min/max, pattern, …). Interpreted dynamically.</summary>
    public string? ValidationRulesJson { get; set; }

    public int Order { get; set; }
}
