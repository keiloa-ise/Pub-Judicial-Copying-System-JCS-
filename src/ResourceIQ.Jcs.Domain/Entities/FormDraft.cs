using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// JC-32 (power-outage resilience): a recoverable draft of a large form, scoped to ONE user and one
/// form key, so different roles editing the same copy never see each other's unsent work. The payload
/// is opaque JSON (the form's client state). Drafts are transient recovery data — never legal record.
/// </summary>
public class FormDraft
{
    /// <summary>Max PayloadJson length in UTF-16 chars (~256 KB). Enforced here, in the service, and by a
    /// DB CHECK constraint on DATALENGTH (bytes = chars * 2 for nvarchar).</summary>
    public const int MaxPayloadJsonLength = 256 * 1024;
    public const int MaxPayloadJsonBytes = MaxPayloadJsonLength * 2;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string FormKey { get; private set; } = string.Empty;
    public Guid? CopyRequestId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset UpdatedUtc { get; private set; }

    private FormDraft() { } // EF

    public static FormDraft Create(
        Guid userId, string role, string formKey, Guid? copyRequestId, string payloadJson, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) throw new DomainException("User is required.");
        var draft = new FormDraft { UserId = userId, CreatedUtc = nowUtc };
        draft.Update(role, formKey, copyRequestId, payloadJson, nowUtc);
        return draft;
    }

    public void Update(string role, string formKey, Guid? copyRequestId, string payloadJson, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new DomainException("Role is required.");
        if (string.IsNullOrWhiteSpace(formKey)) throw new DomainException("Form key is required.");
        if (formKey.Length > 200) throw new DomainException("Form key cannot exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new DomainException("Draft payload is required.");
        if (payloadJson.Length > MaxPayloadJsonLength) throw new DomainException("Draft payload is too large.");

        Role = role.Trim();
        FormKey = formKey.Trim();
        CopyRequestId = copyRequestId;
        PayloadJson = payloadJson;
        UpdatedUtc = nowUtc; // server clock — the tiebreaker for local-vs-server recovery
    }
}
