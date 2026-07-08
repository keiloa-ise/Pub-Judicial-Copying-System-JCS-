using ResourceIQ.Jcs.Application.Admin;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Review;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

public class WorkflowServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly FakeCopyRequestRepository _repo = new();

    // ── Create (FR-06) ──────────────────────────────────────────────────────
    [Fact]
    public async Task Create_allocates_number_and_writes_create_audit()
    {
        var court = Guid.NewGuid();
        var room = Guid.NewGuid();
        var user = new FakeCurrentUser { Role = Role.RegistryHead };
        user.Courts.Add(court);
        var allocator = new FakeAllocator("00000042");
        var queries = new FakeQueries { Room = new RoomDto(room, court, "R-001", "الغرفة الأولى", true, NumberingPolicy.Court, null) };
        var svc = new CreateCopyRequestService(user, _clock, _repo, allocator, new FakeMiscAllocator(), _audit, queries, _uow);

        var req = await svc.HandleAsync(
            new CreateCopyRequestCommand(court, room, null, "case-1", CaseCategory.Normal, CaseUrgency.Suspended, null, null, Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.Equal("00000042", req.CopyNumber);
        Assert.Equal(CopyState.InPreparation, req.State);
        Assert.Equal(1, allocator.Calls);
        Assert.Contains(AuditAction.Create, _audit.Actions);
    }

    [Fact]
    public async Task Create_rejected_for_non_registry_head() // BR-01
    {
        var court = Guid.NewGuid();
        var user = new FakeCurrentUser { Role = Role.Copyist };
        user.Courts.Add(court);
        var svc = new CreateCopyRequestService(user, _clock, _repo, new FakeAllocator(), new FakeMiscAllocator(), _audit, new FakeQueries(), _uow);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.HandleAsync(
            new CreateCopyRequestCommand(court, Guid.NewGuid(), null, "case-1", CaseCategory.Normal, CaseUrgency.Suspended, null, null, Guid.NewGuid(), null),
            CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejected_when_not_assigned_to_court() // BR-06
    {
        var user = new FakeCurrentUser { Role = Role.RegistryHead }; // no court assigned
        var svc = new CreateCopyRequestService(user, _clock, _repo, new FakeAllocator(), new FakeMiscAllocator(), _audit, new FakeQueries(), _uow);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.HandleAsync(
            new CreateCopyRequestCommand(Guid.NewGuid(), Guid.NewGuid(), null, "case-1", CaseCategory.Normal, CaseUrgency.Suspended, null, null, Guid.NewGuid(), null),
            CancellationToken.None));
    }

    // ── Approve (FR-10/11) ──────────────────────────────────────────────────
    [Fact]
    public async Task Reviewer_can_approve_and_audit_is_written()
    {
        var court = Guid.NewGuid();
        var req = SeedUnderReview(court);
        var reviewer = new FakeCurrentUser { Role = Role.Reviewer };
        reviewer.Courts.Add(court);
        var svc = new ReviewService(reviewer, _clock, _repo, _audit, _uow);

        await svc.ApproveAsync(new ApproveCommand(req.Id), CancellationToken.None);

        Assert.Equal(CopyState.Approved, req.State);
        Assert.Contains(AuditAction.Approve, _audit.Actions);
    }

    [Fact]
    public async Task Non_reviewer_cannot_approve() // BR-03
    {
        var court = Guid.NewGuid();
        var req = SeedUnderReview(court);
        var copyist = new FakeCurrentUser { Role = Role.Copyist };
        copyist.Courts.Add(court);
        var svc = new ReviewService(copyist, _clock, _repo, _audit, _uow);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.ApproveAsync(new ApproveCommand(req.Id), CancellationToken.None));
    }

    // ── Unlock (FR-12) ──────────────────────────────────────────────────────
    [Fact]
    public async Task Unlock_requires_a_reason()
    {
        var admin = new FakeCurrentUser { Role = Role.Administrator };
        var svc = new UnlockService(admin, _clock, _repo, _audit, _uow);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.HandleAsync(new UnlockCommand(Guid.NewGuid(), "   "), CancellationToken.None));
    }

    [Fact]
    public async Task Unlock_with_reason_unlocks_and_audits()
    {
        var court = Guid.NewGuid();
        var req = SeedApproved(court);
        var admin = new FakeCurrentUser { Role = Role.Administrator };
        var svc = new UnlockService(admin, _clock, _repo, _audit, _uow);

        await svc.HandleAsync(new UnlockCommand(req.Id, "court order correction"), CancellationToken.None);

        Assert.Equal(CopyState.Unlocked, req.State);
        var entry = Assert.Single(_audit.Entries, e => e.Action == AuditAction.Unlock);
        Assert.Equal("court order correction", entry.Reason);
    }

    [Fact]
    public async Task Non_admin_cannot_unlock() // BR-05
    {
        var reviewer = new FakeCurrentUser { Role = Role.Reviewer };
        var svc = new UnlockService(reviewer, _clock, _repo, _audit, _uow);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.HandleAsync(new UnlockCommand(Guid.NewGuid(), "reason"), CancellationToken.None));
    }

    // ── Deletion (FR-16, BR-09/BR-11) ───────────────────────────────────────
    // متفرق: NO رقم النسخة; carries رقم المتفرق = misc and links to an original (BR-11).
    private CopyRequest SeedMisc(Guid court, int misc = 3, Guid? originalId = null)
    {
        var r = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Miscellaneous, CaseUrgency.Normal, null, "REF-1", originalId ?? Guid.NewGuid(), Guid.NewGuid(), Now);
        r.AssignMiscNumber(misc);
        r.AssignToCopyist(Guid.NewGuid(), Now);
        _repo.Seed(r);
        return r;
    }

    // عادي: carries a رقم النسخة (seq from the number), no link.
    private CopyRequest SeedNormal(Guid court, string number = "2/2026/0005")
    {
        var r = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber(number);
        _repo.Seed(r);
        return r;
    }

    // copyLast/miscLast = what the allocators report as the current last numbers (delete guards).
    private DeleteCopyService MakeDelete(FakeCurrentUser user, int? copyLast = 5, int? miscLast = 3) =>
        new(user, _repo, new FakeAllocator { Last = copyLast }, new FakeMiscAllocator { Last = miscLast }, _audit, _uow);

    [Fact]
    public async Task RegistryHead_deletes_last_misc_and_audit_is_kept()
    {
        var court = Guid.NewGuid();
        var req = SeedMisc(court);
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var svc = MakeDelete(head, miscLast: 3); // miscLast == MiscNumber

        await svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None);

        Assert.False(_repo.Contains(req.Id));                // متفرق removed
        Assert.Contains(AuditAction.Delete, _audit.Actions); // Delete audit appended (and kept)
    }

    [Fact]
    public async Task Cannot_delete_misc_when_not_last_in_scope() // BR-11
    {
        var court = Guid.NewGuid();
        var req = SeedMisc(court, misc: 3);
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var svc = MakeDelete(head, miscLast: 4); // a higher متفرق exists in the scope

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None));
        Assert.True(_repo.Contains(req.Id));
    }

    [Fact]
    public async Task RegistryHead_deletes_latest_normal_copy() // BR-09
    {
        var court = Guid.NewGuid();
        var req = SeedNormal(court);
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var misc = new FakeMiscAllocator { Last = 3 };
        var svc = new DeleteCopyService(head, _repo, new FakeAllocator { Last = 5 }, misc, _audit, _uow); // copyLast == seq 5

        await svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None);

        Assert.False(_repo.Contains(req.Id));
        Assert.Contains(AuditAction.Delete, _audit.Actions);
        Assert.Equal(0, misc.Releases); // رقم المتفرق counter untouched for عادي
    }

    [Fact]
    public async Task Cannot_delete_normal_with_linked_misc() // BR-09: would orphan the متفرق
    {
        var court = Guid.NewGuid();
        var original = SeedNormal(court);
        SeedMisc(court, misc: 1, originalId: original.Id); // a متفرق linked to the original
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var svc = MakeDelete(head, copyLast: 5); // it IS the latest copy, but has a linked متفرق

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(original.Id), CancellationToken.None));
        Assert.True(_repo.Contains(original.Id));
    }

    [Fact]
    public async Task Cannot_delete_when_not_court_year_latest() // no gap in رقم النسخة
    {
        var court = Guid.NewGuid();
        var req = SeedNormal(court);
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var svc = MakeDelete(head, copyLast: 6); // a newer copy exists in the court+year (seq 6 != 5)

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None));
        Assert.True(_repo.Contains(req.Id));
    }

    [Fact]
    public async Task Cannot_delete_after_copyist_accepts_decision()
    {
        var court = Guid.NewGuid();
        var req = SeedNormal(court);
        var copyist = Guid.NewGuid();
        req.AssignToCopyist(copyist, Now);
        req.AcceptByCopyist(copyist, Now);
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);
        var svc = MakeDelete(head, copyLast: 5);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None));
        Assert.True(_repo.Contains(req.Id));
        Assert.DoesNotContain(AuditAction.Delete, _audit.Actions);
    }

    [Fact]
    public async Task Non_registry_head_cannot_delete() // FR-16
    {
        var court = Guid.NewGuid();
        var req = SeedMisc(court);
        var reviewer = new FakeCurrentUser { Role = Role.Reviewer };
        reviewer.Courts.Add(court);
        var svc = MakeDelete(reviewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Registry_head_cannot_delete_outside_their_courts() // BR-06
    {
        var court = Guid.NewGuid();
        var req = SeedMisc(court);
        var head = new FakeCurrentUser { Role = Role.RegistryHead }; // not assigned to `court`
        var svc = MakeDelete(head);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.DeleteAsync(new DeleteCopyRequestCommand(req.Id), CancellationToken.None));
    }

    private CopyRequest SeedUnderReview(Guid court)
    {
        var r = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1), CaseCategory.Normal, CaseUrgency.Suspended, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        var copyist = Guid.NewGuid();
        r.AssignToCopyist(copyist, Now);
        r.AcceptByCopyist(copyist, Now); // FR-07: must accept before submitting
        r.SubmitForReview(Now);
        _repo.Seed(r);
        return r;
    }

    private CopyRequest SeedApproved(Guid court)
    {
        var r = SeedUnderReview(court);
        r.Approve(Guid.NewGuid(), Now);
        return r;
    }
}
