using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Tests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "tester";
    public Role Role { get; set; }
    public bool IsAuthenticated { get; set; } = true;
    public HashSet<Guid> Courts { get; } = new();
    public IReadOnlyCollection<Guid> CourtIds => Courts;
    public bool IsAssignedToCourt(Guid courtId) => Courts.Contains(courtId);
}

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

internal sealed class FakeAllocator(string number = "00000001") : ICopyNumberAllocator
{
    public int Calls { get; private set; }
    public int Releases { get; private set; }
    public int? Last { get; set; } // value PeekLastAsync returns (for delete guards)
    public Task<string> AllocateAsync(Guid courtId, DateOnly reservationDate, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(number);
    }
    public Task ReleaseAsync(Guid courtId, int year, CancellationToken ct) { Releases++; return Task.CompletedTask; }
    public Task<int?> PeekLastAsync(Guid courtId, int year, CancellationToken ct) => Task.FromResult(Last);
}

internal sealed class FakeMiscAllocator(int number = 1) : IMiscNumberAllocator
{
    public int Allocs { get; private set; }
    public int Releases { get; private set; }
    public int? Last { get; set; }
    public Task<int> AllocateAsync(Guid courtId, Guid roomId, int year, CancellationToken ct) { Allocs++; return Task.FromResult(number); }
    public Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct) { Releases++; return Task.CompletedTask; }
    public Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct) => Task.FromResult(Last);
}

internal sealed class FakeAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = new();
    public List<AuditAction> Actions { get; } = new();

    public void Append(Guid copyRequestId, AuditAction action,
        string? beforeJson = null, string? afterJson = null, string? reason = null)
    {
        Actions.Add(action);
        Entries.Add(new AuditEntry
        {
            CopyRequestId = copyRequestId, Action = action,
            BeforeJson = beforeJson, AfterJson = afterJson, Reason = reason,
        });
    }

    public void Append(AuditEntry entry) { Actions.Add(entry.Action); Entries.Add(entry); }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(0); }
    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
}

internal sealed class FakeCopyRequestRepository : ICopyRequestRepository
{
    private readonly Dictionary<Guid, CopyRequest> _store = new();
    public void Seed(CopyRequest r) => _store[r.Id] = r;

    public Task<CopyRequest?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_store.GetValueOrDefault(id));
    public Task<CopyRequest?> GetWithContentAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_store.GetValueOrDefault(id));
    public Task AddAsync(CopyRequest request, CancellationToken ct) { _store[request.Id] = request; return Task.CompletedTask; }
    public Task<bool> AnyLinkedMiscAsync(Guid originalCopyId, CancellationToken ct) =>
        Task.FromResult(_store.Values.Any(x => x.OriginalCopyId == originalCopyId));
    public Task<bool> NormalCaseBaseExistsAsync(Guid courtId, string caseBaseNumber, CancellationToken ct) =>
        Task.FromResult(_store.Values.Any(x => x.CourtId == courtId && x.Category == CaseCategory.Normal && x.CaseBaseNumber == caseBaseNumber));
    public Task<bool> AnyUnacceptedRankedBeforeAsync(Guid copyistId, CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        Task.FromResult(_store.Values.Any(x => x.AssignedCopyistId == copyistId && x.State == CopyState.InPreparation && x.AcceptedUtc == null
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc))));
    public Task<bool> AnyUnderReviewRankedBeforeAsync(IReadOnlyCollection<Guid> courtIds, CaseUrgency urgency, DateTimeOffset createdUtc, CancellationToken ct) =>
        Task.FromResult(_store.Values.Any(x => courtIds.Contains(x.CourtId) && x.State == CopyState.UnderReview
            && (x.Urgency < urgency || (x.Urgency == urgency && x.CreatedUtc < createdUtc))));
    public void Remove(CopyRequest request) => _store.Remove(request.Id);
    public bool Contains(Guid id) => _store.ContainsKey(id);
}

/// <summary>Read-side fake. Only <see cref="GetRoomAsync"/> is exercised by the service tests
/// (room↔court validation on create); the rest are not invoked by those tests.</summary>
internal sealed class FakeQueries : IJcsQueries
{
    public RoomDto? Room { get; set; }
    public Guid? LatestId { get; set; }

    public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct) => Task.FromResult(Room);
    public Task<Guid?> GetLatestCopyRequestIdAsync(IReadOnlyCollection<Guid>? courtIds, CancellationToken ct) => Task.FromResult(LatestId);
    public Task<DeletionTargetsDto> ListDeletionTargetsAsync(IReadOnlyCollection<Guid>? courtIds, int year, CancellationToken ct)
        => Task.FromResult(new DeletionTargetsDto([], []));
    public Task<IReadOnlyList<OriginalCopyOption>> ListSelectableOriginalsAsync(IReadOnlyCollection<Guid>? courtIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OriginalCopyOption>>([]);
    public Task<IReadOnlyList<CopyNumberCounterDto>> ListCopyNumberCountersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CopyNumberCounterDto>>([]);
    public Task<IReadOnlyList<MiscNumberCounterDto>> ListMiscNumberCountersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MiscNumberCounterDto>>([]);

    public Task<IReadOnlyList<CopyRequestListItem>> ListCopyRequestsAsync(CopyRequestFilter filter, CancellationToken ct) => throw new NotImplementedException();
    public Task<CopyRequestDetail?> GetCopyRequestAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid copyRequestId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CourtDto>> ListCourtsAsync(IReadOnlyCollection<Guid>? restrictTo, bool activeOnly, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? courtId, bool activeOnly, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<LookupItem>> ListUsersByRoleAndCourtAsync(Role role, Guid courtId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<LookupItem>> ListJudgesByRoomAsync(Guid roomId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<LookupItem>> ListActiveJudgesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<LookupItem>> ListPanelMemberTitlesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<ParagraphTemplateDto>> ListParagraphTemplatesAsync(bool includeArchived, Guid? formTemplateId, bool onlyForTemplate, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<FormTemplateDto>> ListFormTemplatesAsync(bool activeOnly, CancellationToken ct) => throw new NotImplementedException();
}

/// <summary>Capturing report-query fake: records the (scope, filter) the service passes so tests can
/// assert server-side scoping, and returns empty results.</summary>
internal sealed class FakeReportQueries : IReportQueries
{
    public ReportScope? LastScope { get; private set; }
    public ReportFilter? LastFilter { get; private set; }

    private T Capture<T>(ReportScope scope, ReportFilter filter, T result)
    {
        LastScope = scope; LastFilter = filter; return result;
    }

    public Task<ReportSummaryDto> SummaryAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture(scope, filter, new ReportSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, 0)));
    public Task<IReadOnlyList<CountRow>> CountByCourtAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<IReadOnlyList<CountRow>> CountByRoomAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<IReadOnlyList<CountRow>> CountByCopyistAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<IReadOnlyList<CountRow>> CountByReviewerAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<IReadOnlyList<CountRow>> CountByHeadAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<IReadOnlyList<CountRow>> CountByJudgeAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture<IReadOnlyList<CountRow>>(scope, filter, []));
    public Task<TurnaroundReportDto> TurnaroundAsync(ReportScope scope, ReportFilter filter, CancellationToken ct) =>
        Task.FromResult(Capture(scope, filter, new TurnaroundReportDto([], [])));
    public Task<Paged<CopyRowDto>> CopiesAsync(ReportScope scope, ReportFilter filter, int page, int pageSize, CancellationToken ct) =>
        Task.FromResult(Capture(scope, filter, new Paged<CopyRowDto>([], 0, page, pageSize)));
}
