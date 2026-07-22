using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.FormDrafts;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Tests;

public class FormDraftServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCopyRequestRepository _repo = new();
    private readonly FakeFormDraftStore _drafts = new();

    [Fact]
    public async Task User_can_upsert_read_and_delete_their_own_form_draft()
    {
        var user = new FakeCurrentUser { Role = Role.RegistryHead };
        var svc = new FormDraftService(user, _clock, _drafts, _repo, _uow);
        const string key = "registry-head:create-copy-request:user";

        var saved = await svc.UpsertAsync(new UpsertFormDraftCommand(
            key, "{\"courtId\":\"c1\"}", Now, null), CancellationToken.None);
        var loaded = await svc.GetAsync(key, CancellationToken.None);

        Assert.Equal(key, saved.FormKey);
        Assert.Equal("{\"courtId\":\"c1\"}", loaded?.PayloadJson);
        Assert.Equal(1, _uow.SaveCount);

        await svc.DeleteAsync(key, CancellationToken.None);
        Assert.Null(await svc.GetAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Copyist_cannot_save_a_draft_for_another_copyists_request()
    {
        var court = Guid.NewGuid();
        var copyist = new FakeCurrentUser { Role = Role.Copyist };
        copyist.Courts.Add(court);
        var request = SeedInPreparation(court, assignedCopyistId: Guid.NewGuid());
        var svc = new FormDraftService(copyist, _clock, _drafts, _repo, _uow);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.UpsertAsync(new UpsertFormDraftCommand(
            $"copyist:prepare-copy:{request.Id}:{copyist.Id}",
            "{}",
            Now,
            request.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Copyist_can_delete_own_draft_after_successful_submit_changes_state()
    {
        var court = Guid.NewGuid();
        var copyist = new FakeCurrentUser { Role = Role.Copyist };
        copyist.Courts.Add(court);
        var request = SeedInPreparation(court, copyist.Id);
        var key = $"copyist:prepare-copy:{request.Id}:{copyist.Id}";
        var svc = new FormDraftService(copyist, _clock, _drafts, _repo, _uow);

        await svc.UpsertAsync(new UpsertFormDraftCommand(key, "{\"body\":\"draft\"}", Now, request.Id), CancellationToken.None);
        request.AcceptByCopyist(copyist.Id, Now);
        request.SubmitForReview(Now);

        await svc.DeleteAsync(key, CancellationToken.None);

        Assert.Null(await svc.GetAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Administrator_can_cleanup_old_drafts()
    {
        var admin = new FakeCurrentUser { Role = Role.Administrator };
        var old = FormDraft.Create(admin.Id, Role.Administrator.ToString(), "old", null, "{}", Now.AddDays(-40), Now.AddDays(-40));
        var fresh = FormDraft.Create(admin.Id, Role.Administrator.ToString(), "fresh", null, "{}", Now, Now);
        await _drafts.AddAsync(old, CancellationToken.None);
        await _drafts.AddAsync(fresh, CancellationToken.None);
        var svc = new FormDraftService(admin, _clock, _drafts, _repo, _uow);

        var deleted = await svc.DeleteOlderThanAsync(30, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(await _drafts.GetAsync(admin.Id, "old", CancellationToken.None));
        Assert.NotNull(await _drafts.GetAsync(admin.Id, "fresh", CancellationToken.None));
    }

    private CopyRequest SeedInPreparation(Guid courtId, Guid assignedCopyistId)
    {
        var request = CopyRequest.Create(
            courtId,
            Guid.NewGuid(),
            null,
            Guid.NewGuid().ToString("N"),
            new DateOnly(2026, 7, 20),
            CaseCategory.Normal,
            CaseUrgency.Normal,
            null,
            null,
            null,
            Guid.NewGuid(),
            Now);
        request.AssignNumber("00000001");
        request.AssignToCopyist(assignedCopyistId, Now);
        _repo.Seed(request);
        return request;
    }
}
