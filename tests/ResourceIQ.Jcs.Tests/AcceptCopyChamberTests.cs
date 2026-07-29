using System.Text.Json;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

/// <summary>When the copyist accepts a copy, the fixed الهيئة الحاكمة (chamber) text field is
/// pre-filled ONCE with "المحكمة - الغرفة" and stays editable.</summary>
public class AcceptCopyChamberTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly FakeCopyRequestRepository _repo = new();
    private readonly FakeQueries _queries = new();

    private (CopyRequest copy, FakeCurrentUser copyist) AssignedNotYetAccepted()
    {
        var court = Guid.NewGuid();
        var room = Guid.NewGuid();
        var copyistId = Guid.NewGuid();
        var r = CopyRequest.Create(court, room, null, "case-1", new DateOnly(2026, 6, 1),
            CaseCategory.Normal, CaseUrgency.Normal, null, null, null, Guid.NewGuid(), Now);
        r.AssignNumber("00000001");
        r.AssignToCopyist(copyistId, Now); // → InPreparation, not yet accepted
        _repo.Seed(r);
        var user = new FakeCurrentUser { Role = Role.Copyist, Id = copyistId };
        user.Courts.Add(court);
        return (r, user);
    }

    private AcceptCopyService Service(FakeCurrentUser user) => new(user, _repo, _queries, _clock, _audit, _uow);

    private static string? Chamber(CopyRequest copy)
    {
        using var doc = JsonDocument.Parse(copy.Content!.FieldValuesJson);
        return doc.RootElement.TryGetProperty("chamber", out var v) ? v.GetString() : null;
    }

    [Fact]
    public async Task Accept_prefills_chamber_with_court_and_room()
    {
        var (copy, user) = AssignedNotYetAccepted();
        _queries.CourtRoomNames = new CourtRoomNames("محكمة النقض", "الغرفة الجزائية");

        await Service(user).HandleAsync(new AcceptCopyCommand(copy.Id), default);

        Assert.NotNull(copy.AcceptedUtc);
        Assert.Equal("محكمة النقض - الغرفة الجزائية", Chamber(copy));
    }

    [Fact]
    public async Task Accept_still_succeeds_when_names_are_unavailable()
    {
        var (copy, user) = AssignedNotYetAccepted();
        _queries.CourtRoomNames = null; // court/room not found

        await Service(user).HandleAsync(new AcceptCopyCommand(copy.Id), default);

        Assert.NotNull(copy.AcceptedUtc);         // acceptance is never blocked by the pre-fill
        Assert.Null(copy.Content);                // nothing seeded when names are unavailable
    }
}
