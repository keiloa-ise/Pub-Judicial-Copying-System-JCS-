using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.FormDrafts;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

public class FormDraftServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCopyRequestRepository _repo = new();
    private readonly FakeFormDraftStore _drafts = new();

    private FormDraftService Svc(FakeCurrentUser user) => new(user, _clock, _drafts, _repo, _uow);

    [Fact]
    public async Task User_can_upsert_read_and_delete_their_own_draft()
    {
        var user = new FakeCurrentUser { Role = Role.RegistryHead };
        var svc = Svc(user);
        const string key = "registry-head:create-copy-request:me";

        var saved = await svc.UpsertAsync(new UpsertFormDraftCommand(key, "{\"courtId\":\"c1\"}", null), CancellationToken.None);
        var loaded = await svc.GetAsync(key, CancellationToken.None);

        Assert.Equal(key, saved.FormKey);
        Assert.Equal("{\"courtId\":\"c1\"}", loaded?.PayloadJson);
        Assert.Equal(Now, saved.UpdatedAt);   // server clock

        await svc.DeleteAsync(key, CancellationToken.None);
        Assert.Null(await svc.GetAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Copyist_cannot_draft_another_copyists_request()
    {
        var court = Guid.NewGuid();
        var copyist = new FakeCurrentUser { Role = Role.Copyist };
        copyist.Courts.Add(court);
        var req = SeedInPreparation(court, assignedCopyist: Guid.NewGuid()); // someone else
        var svc = Svc(copyist);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.UpsertAsync(
            new UpsertFormDraftCommand($"copyist:prepare-copy:{req.Id}:{copyist.Id}", "{}", req.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Oversized_payload_is_rejected()
    {
        var svc = Svc(new FakeCurrentUser { Role = Role.RegistryHead });
        var big = "\"" + new string('x', FormDraft.MaxPayloadJsonLength + 1) + "\"";
        await Assert.ThrowsAsync<DomainException>(() => svc.UpsertAsync(
            new UpsertFormDraftCommand("k", big, null), CancellationToken.None));
    }

    [Fact]
    public async Task Blank_payload_is_rejected()
    {
        var svc = Svc(new FakeCurrentUser { Role = Role.RegistryHead });
        await Assert.ThrowsAsync<DomainException>(() => svc.UpsertAsync(
            new UpsertFormDraftCommand("k", "   ", null), CancellationToken.None));
    }

    [Fact]
    public async Task Cleanup_deletes_only_stale_drafts()
    {
        var uid = Guid.NewGuid();
        await _drafts.AddAsync(FormDraft.Create(uid, "RegistryHead", "old", null, "{}", Now.AddDays(-40)), CancellationToken.None);
        await _drafts.AddAsync(FormDraft.Create(uid, "RegistryHead", "fresh", null, "{}", Now), CancellationToken.None);
        var cleanup = new FormDraftCleanupService(_clock, _drafts);

        var deleted = await cleanup.DeleteOlderThanAsync(30, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(await _drafts.GetAsync(uid, "old", CancellationToken.None));
        Assert.NotNull(await _drafts.GetAsync(uid, "fresh", CancellationToken.None));
    }

    private CopyRequest SeedInPreparation(Guid court, Guid assignedCopyist)
    {
        var r = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 7, 20),
            CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("1/2026/0001");
        r.AssignToCopyist(assignedCopyist, Now);
        _repo.Seed(r);
        return r;
    }
}
