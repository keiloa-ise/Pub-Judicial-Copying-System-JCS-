using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A recoverable browser/server draft for large forms. Drafts are scoped to one user and
/// form key, so different roles editing the same copy never see each other's unsent work.
/// </summary>
public class FormDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string FormKey { get; private set; } = string.Empty;
    public Guid? CopyRequestId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset UpdatedUtc { get; private set; }
    public DateTimeOffset? LastSyncedUtc { get; private set; }

    private FormDraft() { }

    public static FormDraft Create(
        Guid userId,
        string role,
        string formKey,
        Guid? copyRequestId,
        string payloadJson,
        DateTimeOffset updatedUtc,
        DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) throw new DomainException("User is required.");
        var draft = new FormDraft
        {
            UserId = userId,
            CreatedUtc = nowUtc,
        };
        draft.Update(role, formKey, copyRequestId, payloadJson, updatedUtc, nowUtc);
        return draft;
    }

    public void Update(
        string role,
        string formKey,
        Guid? copyRequestId,
        string payloadJson,
        DateTimeOffset updatedUtc,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new DomainException("Role is required.");
        if (string.IsNullOrWhiteSpace(formKey)) throw new DomainException("Form key is required.");
        if (formKey.Length > 200) throw new DomainException("Form key cannot exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new DomainException("Draft payload is required.");

        Role = role.Trim();
        FormKey = formKey.Trim();
        CopyRequestId = copyRequestId;
        PayloadJson = payloadJson;
        UpdatedUtc = updatedUtc;
        LastSyncedUtc = nowUtc;
    }
}
