using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

/// <summary>FR-13 authorization: reports are scoped server-side by role + assigned courts (BR-06).
/// These assert the scope the service hands the query layer, that scoped roles cannot widen it via
/// client filters, and that no report runs without a mandatory date range.</summary>
public class ReportServiceTests
{
    // A valid bounded range — required by every report now, so scope tests carry one.
    private static readonly ReportFilter Range = new(FromDate: new DateOnly(2026, 6, 1), ToDate: new DateOnly(2026, 6, 30));

    private static (ReportService svc, FakeReportQueries q, FakeCurrentUser user) Make(Role role, params Guid[] courts)
    {
        var user = new FakeCurrentUser { Role = role };
        foreach (var c in courts) user.Courts.Add(c);
        var q = new FakeReportQueries();
        return (new ReportService(user, q), q, user);
    }

    [Fact]
    public async Task Administrator_is_unrestricted()
    {
        var (svc, q, _) = Make(Role.Administrator, Guid.NewGuid());
        await svc.ByCourtAsync(Range, CancellationToken.None);

        Assert.Null(q.LastScope!.CreatedById);
        Assert.Null(q.LastScope.AssignedCopyistId);
        Assert.Null(q.LastScope.ApprovedById);
        Assert.Null(q.LastScope.CourtIds); // null => all courts
    }

    [Fact]
    public async Task Reviewer_scoped_to_self_and_their_courts()
    {
        var court = Guid.NewGuid();
        var (svc, q, user) = Make(Role.Reviewer, court);
        await svc.SummaryAsync(Range, CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.ApprovedById);
        Assert.Null(q.LastScope.AssignedCopyistId);
        Assert.Null(q.LastScope.CreatedById);
        Assert.Equal(new[] { court }, q.LastScope.CourtIds);
    }

    [Fact]
    public async Task Copyist_scoped_to_self()
    {
        var (svc, q, user) = Make(Role.Copyist, Guid.NewGuid());
        await svc.ByRoomAsync(Range, CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.AssignedCopyistId);
        Assert.Null(q.LastScope.ApprovedById);
    }

    [Fact]
    public async Task RegistryHead_scoped_to_own_created_within_courts()
    {
        var court = Guid.NewGuid();
        var (svc, q, user) = Make(Role.RegistryHead, court);
        await svc.TurnaroundAsync(Range, CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.CreatedById);
        Assert.Equal(new[] { court }, q.LastScope.CourtIds);
    }

    [Fact]
    public async Task Scoped_role_cannot_widen_via_client_actor_filters()
    {
        var (svc, q, _) = Make(Role.Reviewer, Guid.NewGuid());
        // Reviewer tries to query another reviewer's / a copyist's data.
        await svc.ByCopyistAsync(
            Range with { CopyistId = Guid.NewGuid(), ReviewerId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(q.LastFilter!.CopyistId);   // stripped
        Assert.Null(q.LastFilter.ReviewerId);   // stripped
    }

    [Fact]
    public async Task Administrator_keeps_client_actor_filters()
    {
        var copyist = Guid.NewGuid();
        var (svc, q, _) = Make(Role.Administrator);
        await svc.ByCopyistAsync(Range with { CopyistId = copyist }, CancellationToken.None);

        Assert.Equal(copyist, q.LastFilter!.CopyistId);
    }

    [Fact]
    public async Task Copies_pagesize_is_clamped()
    {
        var (svc, q, _) = Make(Role.Administrator);
        var res = await svc.CopiesAsync(Range, page: 0, pageSize: 99999, CancellationToken.None);

        Assert.Equal(1, res.Page);        // page floored to 1
        Assert.Equal(50, res.PageSize);   // oversized pageSize reset to default
    }

    [Fact]
    public async Task Reports_require_a_date_range()
    {
        var (svc, _, _) = Make(Role.Administrator);
        // No FromDate/ToDate → rejected before any query runs.
        await Assert.ThrowsAsync<DomainException>(() => svc.ByCourtAsync(new ReportFilter(), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => svc.ByCourtAsync(new ReportFilter(FromDate: new DateOnly(2026, 6, 1)), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => svc.CopiesAsync(new ReportFilter(), 1, 50, CancellationToken.None));
    }

    [Fact]
    public async Task Detail_reports_reject_an_over_wide_range()
    {
        var (svc, _, _) = Make(Role.Administrator);
        var wide = new ReportFilter(FromDate: new DateOnly(2020, 1, 1), ToDate: new DateOnly(2026, 1, 1)); // ~6 years
        await Assert.ThrowsAsync<DomainException>(() => svc.CopiesAsync(wide, 1, 50, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => svc.JudgeWorkLogAsync(wide, 1, 50, CancellationToken.None));
        // An aggregate report has no span cap — the same wide range is accepted.
        await svc.ByCourtAsync(wide, CancellationToken.None);
    }

    [Fact]
    public async Task Start_date_after_end_date_is_rejected()
    {
        var (svc, _, _) = Make(Role.Administrator);
        var filter = new ReportFilter(
            FromDate: new DateOnly(2026, 6, 10), ToDate: new DateOnly(2026, 6, 1));

        await Assert.ThrowsAsync<DomainException>(() => svc.ByCourtAsync(filter, CancellationToken.None));
    }

    [Fact]
    public async Task Judge_work_log_is_scoped_and_date_validated() // FR-13
    {
        var court = Guid.NewGuid();
        var (svc, q, user) = Make(Role.Reviewer, court);
        await svc.JudgeWorkLogAsync(Range with { CopyistId = Guid.NewGuid() }, page: 1, pageSize: 50, CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.ApprovedById);       // scoped to self
        Assert.Equal(new[] { court }, q.LastScope.CourtIds);    // scoped to their courts
        Assert.Null(q.LastFilter!.CopyistId);                   // client actor filter stripped

        var bad = new ReportFilter(FromDate: new DateOnly(2026, 6, 10), ToDate: new DateOnly(2026, 6, 1));
        await Assert.ThrowsAsync<DomainException>(() => svc.JudgeWorkLogAsync(bad, 1, 50, CancellationToken.None));
    }

    [Fact]
    public async Task Equal_start_and_end_date_is_accepted()
    {
        var (svc, q, _) = Make(Role.Administrator);
        var same = new DateOnly(2026, 6, 10);
        await svc.ByCourtAsync(new ReportFilter(FromDate: same, ToDate: same), CancellationToken.None);

        Assert.Equal(same, q.LastFilter!.FromDate);
        Assert.Equal(same, q.LastFilter.ToDate);
    }
}
