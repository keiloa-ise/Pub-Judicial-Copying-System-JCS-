using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Domain.Enums;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

/// <summary>FR-13 authorization: reports are scoped server-side by role + assigned courts (BR-06).
/// These assert the scope the service hands the query layer, and that scoped roles cannot widen it
/// via client filters.</summary>
public class ReportServiceTests
{
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
        await svc.ByCourtAsync(new ReportFilter(), CancellationToken.None);

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
        await svc.SummaryAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.ApprovedById);
        Assert.Null(q.LastScope.AssignedCopyistId);
        Assert.Null(q.LastScope.CreatedById);
        Assert.Equal(new[] { court }, q.LastScope.CourtIds);
    }

    [Fact]
    public async Task Copyist_scoped_to_self()
    {
        var (svc, q, user) = Make(Role.Copyist, Guid.NewGuid());
        await svc.ByRoomAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.AssignedCopyistId);
        Assert.Null(q.LastScope.ApprovedById);
    }

    [Fact]
    public async Task RegistryHead_scoped_to_own_created_within_courts()
    {
        var court = Guid.NewGuid();
        var (svc, q, user) = Make(Role.RegistryHead, court);
        await svc.TurnaroundAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal(user.Id, q.LastScope!.CreatedById);
        Assert.Equal(new[] { court }, q.LastScope.CourtIds);
    }

    [Fact]
    public async Task Scoped_role_cannot_widen_via_client_actor_filters()
    {
        var (svc, q, _) = Make(Role.Reviewer, Guid.NewGuid());
        // Reviewer tries to query another reviewer's / a copyist's data.
        await svc.ByCopyistAsync(
            new ReportFilter(CopyistId: Guid.NewGuid(), ReviewerId: Guid.NewGuid()), CancellationToken.None);

        Assert.Null(q.LastFilter!.CopyistId);   // stripped
        Assert.Null(q.LastFilter.ReviewerId);   // stripped
    }

    [Fact]
    public async Task Administrator_keeps_client_actor_filters()
    {
        var copyist = Guid.NewGuid();
        var (svc, q, _) = Make(Role.Administrator);
        await svc.ByCopyistAsync(new ReportFilter(CopyistId: copyist), CancellationToken.None);

        Assert.Equal(copyist, q.LastFilter!.CopyistId);
    }

    [Fact]
    public async Task Copies_pagesize_is_clamped()
    {
        var (svc, q, _) = Make(Role.Administrator);
        var res = await svc.CopiesAsync(new ReportFilter(), page: 0, pageSize: 99999, CancellationToken.None);

        Assert.Equal(1, res.Page);        // page floored to 1
        Assert.Equal(50, res.PageSize);   // oversized pageSize reset to default
    }
}
