using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

/// <summary>JC-58: submitting a returned copy records how many words the copyist corrected.</summary>
public class CopyistCorrectionStatTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly FakeCopyRequestRepository _repo = new();
    private readonly FakeCopyCorrectionStatStore _stats = new();

    private (CopyRequest copy, FakeCurrentUser copyist) DraftReadyToSubmit(string afterSectionsJson)
    {
        var court = Guid.NewGuid();
        var copyistId = Guid.NewGuid();
        var r = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        r.AssignToCopyist(copyistId, Now);           // → InPreparation
        r.AcceptByCopyist(copyistId, Now);           // FR-07
        r.UpdateContent(null, "{}", afterSectionsJson, "[]", "[]", "", Now);
        _repo.Seed(r);
        var user = new FakeCurrentUser { Role = Role.Copyist, Id = copyistId };
        user.Courts.Add(court);
        return (r, user);
    }

    private SubmitForReviewService Service(FakeCurrentUser user) =>
        new(user, _clock, _repo, _stats, _audit, _uow);

    [Fact]
    public async Task Submit_after_return_records_corrected_words()
    {
        var (copy, user) = DraftReadyToSubmit("[{\"title\":\"t\",\"text\":\"قرار المحكمة نهائي مبرم\"}]");
        // Reviewer had returned this baseline; the copyist changed "نهائي" → "قطعي" (one substitution).
        _stats.Baseline = new ReturnBaseline(
            "[{\"title\":\"t\",\"text\":\"قرار المحكمة قطعي مبرم\"}]", Now.AddMinutes(-5), Guid.NewGuid());

        await Service(user).HandleAsync(new SubmitForReviewCommand(copy.Id), default);

        var stat = Assert.Single(_stats.Saved);
        Assert.Equal(copy.Id, stat.CopyRequestId);
        Assert.Equal(user.Id, stat.CopyistId);
        Assert.Equal(1, stat.WordsAdded);
        Assert.Equal(1, stat.WordsRemoved);
        Assert.Equal(2, stat.WordsCorrected);
        Assert.Equal(5, stat.TotalWords); // t + قرار + المحكمة + نهائي + مبرم
        Assert.Equal(CopyState.UnderReview, copy.State);
    }

    [Fact]
    public async Task First_submission_without_a_return_records_no_stat()
    {
        var (copy, user) = DraftReadyToSubmit("[{\"title\":\"t\",\"text\":\"مسودة أولى\"}]");
        _stats.Baseline = null; // never returned

        await Service(user).HandleAsync(new SubmitForReviewCommand(copy.Id), default);

        Assert.Empty(_stats.Saved);
        Assert.Equal(CopyState.UnderReview, copy.State);
    }
}
