using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using ResourceIQ.Jcs.Domain.Workflow;

namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// The core aggregate: a judicial copy request. State changes go ONLY through the methods
/// below, each of which validates the transition against <see cref="CopyStateMachine"/>.
/// This is where BR-04 (approved copies are read-only) is enforced in the domain.
/// </summary>
public class CopyRequest
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Sequential copy number (BR-07). Allocated server-side, atomically, by the
    /// application layer's ICopyNumberAllocator and assigned via <see cref="AssignNumber"/>.
    /// Format/uniqueness scope is [OPEN] (decision #1).
    /// </summary>
    public string? CopyNumber { get; private set; }

    public Guid CourtId { get; private set; }

    /// <summary>
    /// The room (غرفة) within the court that this request targets. Chosen by the Registry Head at
    /// creation; the judging panel is later picked from this room's judges. Court scoping and copy
    /// numbering remain per-court (CourtId), so both are kept and validated to be consistent.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>قيد الدعوى — the case-filing date (optional). Replaces the former "مرجع الحكم".</summary>
    public DateOnly? CaseFilingDate { get; private set; }
    public string CaseBaseNumber { get; private set; } = string.Empty;
    public DateOnly ReservationDate { get; private set; }

    /// <summary>Classifications chosen by the Registry Head at creation (FR-06).</summary>
    public CaseCategory Category { get; private set; }
    public CaseUrgency Urgency { get; private set; }

    /// <summary>رقم طلب الاستعجال — required only when <see cref="Urgency"/> is مستعجل (Expedited).</summary>
    public string? ExpediteRequestNumber { get; private set; }

    /// <summary>رقم المرجع — an optional external reference for متفرق copies (FR-06, BR-11).</summary>
    public string? ReferenceNumber { get; private set; }

    /// <summary>
    /// النسخة الأصلية — for a متفرق copy, the id of the Approved عادي copy it is based on (BR-11).
    /// A متفرق does NOT get its own رقم النسخة; it carries only a رقم المتفرق and links here. Null
    /// for عادي copies. One original copy may have many متفرق copies linked to it.
    /// </summary>
    public Guid? OriginalCopyId { get; private set; }

    /// <summary>رقم المتفرق — an extra sequential number allocated server-side for متفرق copies
    /// (per-room for جزائية courts, per-court otherwise; resets yearly). Null for other categories.</summary>
    public int? MiscNumber { get; private set; }

    public CopyState State { get; private set; } = CopyState.Created;

    public Guid? AssignedCopyistId { get; private set; }
    public Guid CreatedById { get; private set; }

    /// <summary>When the assigned Copyist accepted the copy (FR-07). The Copyist must accept before
    /// editing; the timestamp feeds reporting (time-to-acceptance). Null until accepted.</summary>
    public DateTimeOffset? AcceptedUtc { get; private set; }
    public Guid? AcceptedById { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset? UpdatedUtc { get; private set; }
    public DateTimeOffset? ApprovedUtc { get; private set; }
    public Guid? ApprovedById { get; private set; }

    public CopyContent? Content { get; private set; }

    private CopyRequest() { } // EF

    /// <summary>
    /// Create a request in the <see cref="CopyState.Created"/> state. The copy number is
    /// assigned separately by the service (inside the same transaction) once allocated.
    /// </summary>
    public static CopyRequest Create(
        Guid courtId, Guid roomId, DateOnly? caseFilingDate, string caseBaseNumber,
        DateOnly reservationDate, CaseCategory category, CaseUrgency urgency,
        string? expediteRequestNumber, string? referenceNumber, Guid? originalCopyId,
        Guid createdById, DateTimeOffset nowUtc)
    {
        if (courtId == Guid.Empty) throw new DomainException("Court is required.");
        if (roomId == Guid.Empty) throw new DomainException("Room is required.");
        if (string.IsNullOrWhiteSpace(caseBaseNumber)) throw new DomainException("Case base number is required.");
        if (!Enum.IsDefined(category)) throw new DomainException("A valid category (عادي/متفرق) is required.");
        if (!Enum.IsDefined(urgency)) throw new DomainException("A valid status (عادي/موقوف/مستعجل) is required.");
        // FR-06: an expedite-request number is mandatory when the status is مستعجل (Expedited).
        if (urgency == CaseUrgency.Expedited && string.IsNullOrWhiteSpace(expediteRequestNumber))
            throw new DomainException("رقم طلب الاستعجال مطلوب عند اختيار الحالة «مستعجل».");
        // BR-11: a متفرق copy must be based on an existing (Approved) عادي copy; عادي carries no link.
        if (category == CaseCategory.Miscellaneous && (originalCopyId is null || originalCopyId == Guid.Empty))
            throw new DomainException("القرار المتفرق يجب أن يستند إلى نسخة أصلية معتمدة.");
        if (category != CaseCategory.Miscellaneous && originalCopyId is not null)
            throw new DomainException("النسخة الأصلية تُحدَّد للقرارات المتفرقة فقط.");

        return new CopyRequest
        {
            CourtId = courtId,
            RoomId = roomId,
            CaseFilingDate = caseFilingDate,
            CaseBaseNumber = caseBaseNumber.Trim(),
            ReservationDate = reservationDate,
            Category = category,
            Urgency = urgency,
            ExpediteRequestNumber = urgency == CaseUrgency.Expedited ? expediteRequestNumber!.Trim() : null,
            // رقم المرجع is now optional (BR-11); kept for متفرق only.
            ReferenceNumber = category == CaseCategory.Miscellaneous && !string.IsNullOrWhiteSpace(referenceNumber)
                ? referenceNumber.Trim() : null,
            OriginalCopyId = category == CaseCategory.Miscellaneous ? originalCopyId : null,
            CreatedById = createdById,
            State = CopyState.Created,
            CreatedUtc = nowUtc,
        };
    }

    /// <summary>Assigns the auto-allocated رقم المتفرق (متفرق copies only). Set once, at creation.</summary>
    public void AssignMiscNumber(int miscNumber)
    {
        if (MiscNumber is not null) throw new DomainException("Misc number is already assigned.");
        if (Category != CaseCategory.Miscellaneous) throw new DomainException("Misc number applies to متفرق copies only.");
        MiscNumber = miscNumber;
    }

    /// <summary>Assigns the allocated sequential number. Set once, at creation time.</summary>
    public void AssignNumber(string copyNumber)
    {
        if (!string.IsNullOrEmpty(CopyNumber))
            throw new DomainException("Copy number is already assigned.");
        if (string.IsNullOrWhiteSpace(copyNumber))
            throw new DomainException("Copy number cannot be blank.");
        CopyNumber = copyNumber;
    }

    /// <summary>Created → InPreparation: route to the assigned copyist's queue.</summary>
    public void AssignToCopyist(Guid copyistId, DateTimeOffset nowUtc)
    {
        CopyStateMachine.EnsureTransition(State, CopyState.InPreparation);
        AssignedCopyistId = copyistId;
        State = CopyState.InPreparation;
        UpdatedUtc = nowUtc;
    }

    /// <summary>FR-07: the assigned Copyist accepts the copy before editing it. Records the acceptance
    /// time/actor (used in reports). Allowed once, while In preparation and not yet accepted.</summary>
    public void AcceptByCopyist(Guid copyistId, DateTimeOffset nowUtc)
    {
        if (State != CopyState.InPreparation)
            throw new DomainException("لا يمكن قبول القرار إلا وهو «قيد التحضير».");
        if (AcceptedUtc is not null)
            throw new DomainException("تم قبول هذا القرار مسبقاً.");
        AcceptedById = copyistId;
        AcceptedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>FR-06: a non-approved copy may be escalated to مستعجل at any time (Registry Head),
    /// which requires an expedite-request number and raises its work-queue priority (BR-10).</summary>
    public void EscalateToExpedited(string expediteRequestNumber, DateTimeOffset nowUtc)
    {
        if (State == CopyState.Approved)
            throw new DomainException("لا يمكن تغيير حالة قرار معتمد.");
        if (Urgency == CaseUrgency.Suspended)
            throw new DomainException("لا يمكن تخفيض حالة قرار موقوف إلى مستعجل.");
        if (string.IsNullOrWhiteSpace(expediteRequestNumber))
            throw new DomainException("رقم طلب الاستعجال مطلوب عند التصعيد إلى «مستعجل».");
        Urgency = CaseUrgency.Expedited;
        ExpediteRequestNumber = expediteRequestNumber.Trim();
        UpdatedUtc = nowUtc;
    }

    /// <summary>FR-06: a non-approved copy may be escalated to موقوف at any time (Registry Head).
    /// This is the highest work-queue priority and does not require an expedite-request number.</summary>
    public void EscalateToSuspended(DateTimeOffset nowUtc)
    {
        if (State == CopyState.Approved)
            throw new DomainException("لا يمكن تغيير حالة قرار معتمد.");
        Urgency = CaseUrgency.Suspended;
        ExpediteRequestNumber = null;
        UpdatedUtc = nowUtc;
    }

    /// <summary>
    /// Guards every content write. A copy is editable while In preparation, or after an
    /// Administrator unlock (state Unlocked) so the assigned copyist can correct it and
    /// re-submit (decision #3). Approved copies stay read-only (BR-04).
    /// </summary>
    public void EnsureEditable()
    {
        if (State is not (CopyState.InPreparation or CopyState.Unlocked))
            throw new DomainException($"Copy is not editable in state {State} (BR-04).");
    }

    /// <summary>
    /// Replaces the editable content (form field values + legal body). Only permitted while
    /// in preparation (BR-04). Legal text is stored verbatim — never truncated or auto-corrected.
    /// </summary>
    public void UpdateContent(
        Guid? formTemplateId, string fieldValuesJson, string sectionsJson, string dissentSectionsJson, string body, DateTimeOffset nowUtc)
    {
        EnsureEditable();
        EnsureAccepted(); // FR-07: the Copyist must accept the copy before editing it.
        Content ??= new CopyContent { CopyRequestId = Id };
        Content.FormTemplateId = formTemplateId;
        Content.FieldValuesJson = string.IsNullOrWhiteSpace(fieldValuesJson) ? "{}" : fieldValuesJson;
        Content.SectionsJson = string.IsNullOrWhiteSpace(sectionsJson) ? "[]" : sectionsJson;
        Content.DissentSectionsJson = string.IsNullOrWhiteSpace(dissentSectionsJson) ? "[]" : dissentSectionsJson;
        Content.Body = body ?? string.Empty;
        UpdatedUtc = nowUtc;
    }

    /// <summary>FR-07: a copy cannot be edited or submitted until the assigned Copyist accepts it.</summary>
    private void EnsureAccepted()
    {
        if (AcceptedUtc is null)
            throw new DomainException("يجب قبول القرار أولاً قبل البدء بالتحرير.");
    }

    /// <summary>
    /// FR-10: the Reviewer corrects content directly while the copy is UnderReview — without
    /// returning it to the copyist — and then approves it. The state does NOT change here (it
    /// stays UnderReview); only <see cref="Approve"/> or a later admin <see cref="Unlock"/> move
    /// it onward. This is the ONLY content write permitted outside <see cref="EnsureEditable"/>;
    /// the Reviewer role + court are enforced by the application service, and an Edit audit entry
    /// (actor = reviewer) is written there. Legal text is stored verbatim — never auto-corrected.
    /// </summary>
    public void CorrectByReviewer(
        Guid? formTemplateId, string fieldValuesJson, string sectionsJson, string dissentSectionsJson, string body, DateTimeOffset nowUtc)
    {
        if (State != CopyState.UnderReview)
            throw new DomainException($"Reviewer correction is only allowed while the copy is under review (was {State}).");
        Content ??= new CopyContent { CopyRequestId = Id };
        Content.FormTemplateId = formTemplateId;
        Content.FieldValuesJson = string.IsNullOrWhiteSpace(fieldValuesJson) ? "{}" : fieldValuesJson;
        Content.SectionsJson = string.IsNullOrWhiteSpace(sectionsJson) ? "[]" : sectionsJson;
        Content.DissentSectionsJson = string.IsNullOrWhiteSpace(dissentSectionsJson) ? "[]" : dissentSectionsJson;
        Content.Body = body ?? string.Empty;
        UpdatedUtc = nowUtc;
    }

    /// <summary>
    /// Copyist submits for review: InPreparation → UnderReview, or Unlocked → UnderReview when
    /// re-submitting a copy that an Administrator unlocked (decision #3).
    /// </summary>
    public void SubmitForReview(DateTimeOffset nowUtc)
    {
        EnsureAccepted(); // FR-07: cannot submit a copy the Copyist hasn't accepted.
        CopyStateMachine.EnsureTransition(State, CopyState.UnderReview);
        State = CopyState.UnderReview;
        UpdatedUtc = nowUtc;
    }

    /// <summary>UnderReview → Approved (locked). Records reviewer identity and time (FR-10/11).</summary>
    public void Approve(Guid reviewerId, DateTimeOffset nowUtc)
    {
        CopyStateMachine.EnsureTransition(State, CopyState.Approved);
        State = CopyState.Approved;
        ApprovedById = reviewerId;
        ApprovedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>
    /// UnderReview → InPreparation (reviewer returns for correction, 7B).
    /// [OPEN] decision #2: no cap is enforced on the number of return cycles yet.
    /// </summary>
    public void ReturnForCorrection(DateTimeOffset nowUtc)
    {
        CopyStateMachine.EnsureTransition(State, CopyState.InPreparation);
        State = CopyState.InPreparation;
        UpdatedUtc = nowUtc;
    }

    /// <summary>
    /// Approved → Unlocked (Administrator only, FR-12). The mandatory reason and audit entry
    /// are written by the application service in the same transaction. The assigned copyist may
    /// then re-edit and re-submit it (Unlocked → UnderReview, decision #3).
    /// </summary>
    public void Unlock(DateTimeOffset nowUtc)
    {
        CopyStateMachine.EnsureTransition(State, CopyState.Unlocked);
        State = CopyState.Unlocked;
        UpdatedUtc = nowUtc;
    }
}
