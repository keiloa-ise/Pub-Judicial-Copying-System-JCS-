using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

public class CopyRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CopyRequest Approved()
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        var copyist = Guid.NewGuid();
        r.AssignToCopyist(copyist, Now);           // → InPreparation
        r.AcceptByCopyist(copyist, Now);           // FR-07: accept before editing/submitting
        r.SubmitForReview(Now);                    // → UnderReview
        r.Approve(Guid.NewGuid(), Now);            // → Approved (locked)
        return r;
    }

    [Fact]
    public void Happy_path_reaches_approved_and_records_reviewer()
    {
        var r = Approved();
        Assert.Equal(CopyState.Approved, r.State);
        Assert.NotNull(r.ApprovedById);
        Assert.NotNull(r.ApprovedUtc);
    }

    [Fact]
    public void Approved_copy_rejects_content_edits() // BR-04
    {
        var r = Approved();
        Assert.Throws<DomainException>(() => r.UpdateContent(null, "{}", "[]", "[]", "new text", Now));
        Assert.Throws<DomainException>(() => r.EnsureEditable());
    }

    [Fact]
    public void Approved_copy_cannot_be_resubmitted_or_returned()
    {
        var r = Approved();
        Assert.Throws<DomainException>(() => r.SubmitForReview(Now));
        Assert.Throws<DomainException>(() => r.ReturnForCorrection(Now));
    }

    [Fact]
    public void Unlock_then_copyist_can_reedit_and_resubmit() // decision #3
    {
        var r = Approved();
        r.Unlock(Now);
        Assert.Equal(CopyState.Unlocked, r.State);

        // Assigned copyist may edit an unlocked copy...
        r.UpdateContent(null, "{}", "[{\"title\":\"t\",\"text\":\"fix\"}]", "[]", "", Now);
        // ...and re-submit it to the reviewer (Unlocked → UnderReview).
        r.SubmitForReview(Now);
        Assert.Equal(CopyState.UnderReview, r.State);

        // The reviewer can then approve again.
        r.Approve(Guid.NewGuid(), Now);
        Assert.Equal(CopyState.Approved, r.State);
    }

    private static CopyRequest UnderReview()
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        var copyist = Guid.NewGuid();
        r.AssignToCopyist(copyist, Now);        // → InPreparation
        r.AcceptByCopyist(copyist, Now);        // FR-07: accept before submit
        r.SubmitForReview(Now);                 // → UnderReview
        return r;
    }

    [Fact]
    public void Reviewer_corrects_in_place_and_stays_under_review() // FR-10 / BR-08
    {
        var r = UnderReview();
        r.CorrectByReviewer(null, "{}", "[{\"title\":\"t\",\"text\":\"reviewer fix\"}]", "[]", "", Now);
        Assert.Equal(CopyState.UnderReview, r.State); // no state change
        Assert.Contains("reviewer fix", r.Content!.SectionsJson);

        // The reviewer then approves normally.
        r.Approve(Guid.NewGuid(), Now);
        Assert.Equal(CopyState.Approved, r.State);
    }

    [Theory]
    [InlineData(CopyState.Created)]
    [InlineData(CopyState.InPreparation)]
    [InlineData(CopyState.Approved)]
    [InlineData(CopyState.Unlocked)]
    public void Reviewer_correction_rejected_outside_under_review(CopyState state) // BR-08
    {
        var r = Approved();
        if (state == CopyState.Unlocked) r.Unlock(Now);
        // Approved/Unlocked are reached above; Created/InPreparation use a fresh request.
        if (state is CopyState.Created or CopyState.InPreparation)
        {
            r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
            r.AssignNumber("00000002");
            if (state == CopyState.InPreparation) r.AssignToCopyist(Guid.NewGuid(), Now);
        }
        Assert.Equal(state, r.State);
        Assert.Throws<DomainException>(() => r.CorrectByReviewer(null, "{}", "[]", "[]", "x", Now));
    }

    [Fact]
    public void Expedited_status_requires_an_expedite_request_number() // FR-06
    {
        // مستعجل (Expedited) without a number → rejected.
        Assert.Throws<DomainException>(() => CopyRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Expedited, null, null, null, Guid.NewGuid(), Now));

        // With a number → ok, and it is stored.
        var r = CopyRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Expedited, "EXP-42", null, null, Guid.NewGuid(), Now);
        Assert.Equal("EXP-42", r.ExpediteRequestNumber);

        // موقوف (Suspended) ignores any number (kept null).
        var s = CopyRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Suspended, "ignored", null, null, Guid.NewGuid(), Now);
        Assert.Null(s.ExpediteRequestNumber);
    }

    [Fact]
    public void Miscellaneous_requires_an_original_copy() // BR-11
    {
        // متفرق without an original copy → rejected (رقم المرجع is now optional).
        Assert.Throws<DomainException>(() => CopyRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Miscellaneous, CaseUrgency.Normal, null, "REF-9", null, Guid.NewGuid(), Now));

        // With an original copy → ok; the link is stored and رقم المرجع is kept when provided.
        var original = Guid.NewGuid();
        var r = CopyRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Miscellaneous, CaseUrgency.Normal, null, "REF-9", original, Guid.NewGuid(), Now);
        Assert.Equal(original, r.OriginalCopyId);
        Assert.Equal("REF-9", r.ReferenceNumber);

        r.AssignMiscNumber(5);
        Assert.Equal(5, r.MiscNumber);
        Assert.Throws<DomainException>(() => r.AssignMiscNumber(6)); // set once
    }

    [Fact]
    public void Copy_number_is_assigned_once()
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        Assert.Throws<DomainException>(() => r.AssignNumber("00000002"));
    }

    [Fact]
    public void Editing_allowed_only_in_preparation()
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        var copyist = Guid.NewGuid();
        r.AssignToCopyist(copyist, Now);
        r.AcceptByCopyist(copyist, Now); // FR-07: accept before editing
        r.UpdateContent(null, "{}", "[{\"title\":\"t\",\"text\":\"draft\"}]", "[]", "draft body", Now); // ok in preparation
        Assert.Equal("draft body", r.Content!.Body);
        Assert.Contains("draft", r.Content!.SectionsJson);

        r.SubmitForReview(Now); // → UnderReview
        Assert.Throws<DomainException>(() => r.UpdateContent(null, "{}", "[]", "[]", "x", Now));
    }

    [Fact]
    public void Cannot_edit_or_submit_before_acceptance() // FR-07
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("1/2026/0001");
        r.AssignToCopyist(Guid.NewGuid(), Now); // assigned but NOT accepted
        Assert.Throws<DomainException>(() => r.UpdateContent(null, "{}", "[]", "[]", "x", Now));
        Assert.Throws<DomainException>(() => r.SubmitForReview(Now));
    }

    [Fact]
    public void Escalate_to_expedited_requires_number_and_not_approved() // FR-06
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("1/2026/0001");
        Assert.Throws<DomainException>(() => r.EscalateToExpedited("", Now)); // number required
        r.EscalateToExpedited("EXP-9", Now);
        Assert.Equal(CaseUrgency.Expedited, r.Urgency);
        Assert.Equal("EXP-9", r.ExpediteRequestNumber);

        var ap = Approved();
        Assert.Throws<DomainException>(() => ap.EscalateToExpedited("EXP-1", Now)); // approved → rejected
    }

    [Fact]
    public void Suspended_copy_cannot_be_downgraded_to_expedited() // FR-06 priority safety
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("1/2026/0001");

        Assert.Throws<DomainException>(() => r.EscalateToExpedited("EXP-1", Now));
        Assert.Equal(CaseUrgency.Suspended, r.Urgency);
        Assert.Null(r.ExpediteRequestNumber);
    }

    [Fact]
    public void Escalate_to_suspended_requires_not_approved_and_clears_expedite_number() // FR-06
    {
        var r = CopyRequest.Create(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Expedited, "EXP-9", null, null, Guid.NewGuid(), Now);
        r.AssignNumber("1/2026/0001");

        r.EscalateToSuspended(Now);

        Assert.Equal(CaseUrgency.Suspended, r.Urgency);
        Assert.Null(r.ExpediteRequestNumber);

        var ap = Approved();
        Assert.Throws<DomainException>(() => ap.EscalateToSuspended(Now)); // approved → rejected
    }
}
