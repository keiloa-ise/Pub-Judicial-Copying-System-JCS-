using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Application.Review;
using ResourceIQ.Jcs.Application.Security;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

/// <summary>BR-06 room-level scope: Copyists/Reviewers are scoped to their assigned ROOMS,
/// Registry Heads to their COURTS, Administrators unrestricted.</summary>
public class RoomScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(Role.Copyist)]
    [InlineData(Role.Reviewer)]
    public void Copyist_and_reviewer_are_scoped_to_assigned_room(Role role)
    {
        var court = Guid.NewGuid();
        var room = Guid.NewGuid();
        var otherRoom = Guid.NewGuid();
        var u = new FakeCurrentUser { Role = role };
        u.Rooms.Add(room);

        Guard.RequireCopyScope(u, court, room); // assigned room → allowed
        Assert.Throws<ForbiddenException>(() => Guard.RequireCopyScope(u, court, otherRoom));
    }

    [Fact]
    public void Registry_head_is_scoped_by_court_regardless_of_room()
    {
        var court = Guid.NewGuid();
        var otherCourt = Guid.NewGuid();
        var head = new FakeCurrentUser { Role = Role.RegistryHead };
        head.Courts.Add(court);

        Guard.RequireCopyScope(head, court, Guid.NewGuid());  // any room in the assigned court → allowed
        Assert.Throws<ForbiddenException>(() => Guard.RequireCopyScope(head, otherCourt, Guid.NewGuid()));
    }

    [Fact]
    public void Administrator_is_unrestricted()
    {
        var admin = new FakeCurrentUser { Role = Role.Administrator };
        Guard.RequireCopyScope(admin, Guid.NewGuid(), Guid.NewGuid()); // no throw
    }

    [Fact]
    public async Task Reviewer_cannot_approve_a_copy_in_an_unassigned_room()
    {
        var court = Guid.NewGuid();
        var repo = new FakeCopyRequestRepository();
        var copy = CopyRequest.Create(court, Guid.NewGuid(), null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        copy.AssignNumber("00000001");
        var copyistId = Guid.NewGuid();
        copy.AssignToCopyist(copyistId, Now);
        copy.AcceptByCopyist(copyistId, Now);
        copy.SubmitForReview(Now); // → UnderReview
        repo.Seed(copy);

        // Reviewer assigned to the court but to a DIFFERENT room than the copy's.
        var reviewer = new FakeCurrentUser { Role = Role.Reviewer };
        reviewer.Courts.Add(court);
        reviewer.Rooms.Add(Guid.NewGuid()); // not copy.RoomId
        var svc = new ReviewService(reviewer, new FakeClock(Now), repo, new FakeAuditWriter(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.ApproveAsync(new ApproveCommand(copy.Id), CancellationToken.None));
        Assert.Equal(CopyState.UnderReview, copy.State); // unchanged
    }
}
