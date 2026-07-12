using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>EF-backed read projections. No tracking — these are read-only DTO queries.</summary>
public sealed class JcsQueries(JcsDbContext db) : IJcsQueries
{
    public async Task<IReadOnlyList<CopyRequestListItem>> ListCopyRequestsAsync(CopyRequestFilter filter, CancellationToken ct)
    {
        var states = filter.States?.ToArray();
        var courts = filter.CourtIds?.ToArray();

        var q = from cr in db.CopyRequests.AsNoTracking()
                join c in db.Courts on cr.CourtId equals c.Id
                join rm in db.Rooms on cr.RoomId equals rm.Id
                join uu in db.Users on cr.AssignedCopyistId equals uu.Id into uj
                from u in uj.DefaultIfEmpty()
                select new { cr, CourtName = c.Name, RoomName = rm.Name, CopyistName = (string?)(u != null ? u.DisplayName : null) };

        if (states is { Length: > 0 }) q = q.Where(x => states.Contains(x.cr.State));
        if (filter.AssignedCopyistId is { } cp) q = q.Where(x => x.cr.AssignedCopyistId == cp);
        if (filter.CreatedById is { } cb) q = q.Where(x => x.cr.CreatedById == cb);
        // null courts => no restriction (admin); a non-null list restricts (empty => no rows).
        if (courts is not null) q = q.Where(x => courts.Contains(x.cr.CourtId));

        // Advanced-search narrowing
        if (!string.IsNullOrWhiteSpace(filter.CopyNumber))
            q = q.Where(x => x.cr.CopyNumber != null && x.cr.CopyNumber.Contains(filter.CopyNumber));
        if (!string.IsNullOrWhiteSpace(filter.CaseBaseNumber))
            q = q.Where(x => x.cr.CaseBaseNumber.Contains(filter.CaseBaseNumber));
        if (filter.FromReservation is { } from) q = q.Where(x => x.cr.ReservationDate >= from);
        if (filter.ToReservation is { } to) q = q.Where(x => x.cr.ReservationDate <= to);

        return await q
            // Work-queue execution priority: موقوف (Suspended) first, then مستعجل (Expedited),
            // then the rest; within a tier, OLDEST first (by creation) — acceptance must follow this order.
            .OrderByDescending(x => x.cr.Urgency == CaseUrgency.Suspended)
            .ThenByDescending(x => x.cr.Urgency == CaseUrgency.Expedited)
            .ThenBy(x => x.cr.CreatedUtc)
            .Select(x => new CopyRequestListItem(
                x.cr.Id, x.cr.CopyNumber, x.cr.State, x.cr.CourtId, x.CourtName,
                x.cr.RoomId, x.RoomName,
                x.cr.CaseBaseNumber, x.cr.CaseFilingDate, x.cr.ReservationDate,
                x.cr.Category, x.cr.Urgency, x.cr.ExpediteRequestNumber, x.cr.MiscNumber,
                x.cr.AssignedCopyistId, x.CopyistName, x.cr.CreatedUtc, x.cr.AcceptedUtc))
            .ToListAsync(ct);
    }

    public async Task<CopyRequestDetail?> GetCopyRequestAsync(Guid id, CancellationToken ct)
    {
        var row = await (from cr in db.CopyRequests.AsNoTracking().Include(c => c.Content)
                         join c in db.Courts on cr.CourtId equals c.Id
                         join rm in db.Rooms on cr.RoomId equals rm.Id
                         join uu in db.Users on cr.AssignedCopyistId equals uu.Id into uj
                         from u in uj.DefaultIfEmpty()
                         join occ in db.CopyRequests on cr.OriginalCopyId equals occ.Id into ocj
                         from orig in ocj.DefaultIfEmpty()
                         where cr.Id == id
                         select new
                         {
                             cr, CourtName = c.Name, RoomName = rm.Name,
                             CopyistName = (string?)(u != null ? u.DisplayName : null),
                             OriginalCopyNumber = (string?)(orig != null ? orig.CopyNumber : null),
                         }).FirstOrDefaultAsync(ct);
        if (row is null) return null;

        // BR-11: متفرق copies linked to this (original) copy, shown under its detail.
        var linked = await db.CopyRequests.AsNoTracking()
            .Where(x => x.OriginalCopyId == id)
            .OrderBy(x => x.MiscNumber)
            .Select(x => new LinkedMiscDto(x.Id, x.MiscNumber, x.ReferenceNumber, x.State, x.ReservationDate))
            .ToListAsync(ct);

        var cr2 = row.cr;
        return new CopyRequestDetail(
            cr2.Id, cr2.CopyNumber, cr2.State, cr2.CourtId, row.CourtName,
            cr2.RoomId, row.RoomName,
            cr2.CaseBaseNumber, cr2.CaseFilingDate, cr2.ReservationDate,
            cr2.Category, cr2.Urgency, cr2.ExpediteRequestNumber, cr2.ReferenceNumber, cr2.MiscNumber,
            cr2.AssignedCopyistId, row.CopyistName,
            cr2.Content != null ? cr2.Content.FormTemplateId : null,
            cr2.Content != null ? cr2.Content.FieldValuesJson : "{}",
            cr2.Content != null ? cr2.Content.SectionsJson : "[]",
            cr2.Content != null ? cr2.Content.DissentSectionsJson : "[]",
            cr2.Content != null ? cr2.Content.RebuttalSectionsJson : "[]",
            cr2.Content != null ? cr2.Content.Body : "",
            cr2.CreatedUtc, cr2.ApprovedUtc, cr2.AcceptedUtc,
            cr2.OriginalCopyId, row.OriginalCopyNumber, linked);
    }

    public async Task<Guid?> GetLatestCopyRequestIdAsync(IReadOnlyCollection<Guid>? courtIds, CancellationToken ct)
    {
        var ids = courtIds?.ToArray();
        var q = db.CopyRequests.AsNoTracking().AsQueryable();
        if (ids is not null) q = q.Where(cr => ids.Contains(cr.CourtId)); // empty list => no rows => null
        return await q.OrderByDescending(cr => cr.CreatedUtc)
            .Select(cr => (Guid?)cr.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DeletionTargetsDto> ListDeletionTargetsAsync(
        IReadOnlyCollection<Guid>? courtIds, int year, CancellationToken ct)
    {
        var ids = courtIds?.ToArray();

        // ── عادي: the latest copy per court for the year (BR-09). ──
        var nq = from cr in db.CopyRequests.AsNoTracking()
                 join c in db.Courts on cr.CourtId equals c.Id
                 join rm in db.Rooms on cr.RoomId equals rm.Id
                 where cr.ReservationDate.Year == year && cr.Category == CaseCategory.Normal && cr.CopyNumber != null
                 select new { cr.Id, CopyNumber = cr.CopyNumber!, cr.CourtId, CourtName = c.Name, RoomName = rm.Name, cr.State, cr.CreatedUtc };
        if (ids is not null) nq = nq.Where(x => ids.Contains(x.CourtId));
        var nrows = await nq.ToListAsync(ct);
        var latest = nrows.GroupBy(r => r.CourtId).Select(g => g.OrderByDescending(x => x.CreatedUtc).First()).ToList();

        // Which of those have linked متفرق copies (then they can't be deleted yet).
        var latestIds = latest.Select(x => x.Id).ToArray();
        var linkedSet = (await db.CopyRequests.AsNoTracking()
            .Where(x => x.OriginalCopyId != null && latestIds.Contains(x.OriginalCopyId!.Value))
            .Select(x => x.OriginalCopyId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        var normals = latest
            .Select(x => new DeletableCopyDto(x.CourtId, x.CourtName, x.Id, x.CopyNumber, x.RoomName, x.State, linkedSet.Contains(x.Id)))
            .OrderBy(d => d.CourtName).ToList();

        // ── متفرق: the last one per numbering scope for the year (BR-11). ──
        var mq = from cr in db.CopyRequests.AsNoTracking()
                 join c in db.Courts on cr.CourtId equals c.Id
                 join rm in db.Rooms on cr.RoomId equals rm.Id
                 join occ in db.CopyRequests on cr.OriginalCopyId equals occ.Id into ocj
                 from orig in ocj.DefaultIfEmpty()
                 where cr.ReservationDate.Year == year && cr.Category == CaseCategory.Miscellaneous && cr.MiscNumber != null
                 select new
                 {
                     cr.Id, Misc = cr.MiscNumber!.Value, cr.CourtId, CourtName = c.Name, cr.RoomId, RoomName = rm.Name,
                     rm.NumberingPolicy, rm.NumberingLevel, cr.State, cr.ReferenceNumber,
                     OriginalCopyNumber = (string?)(orig != null ? orig.CopyNumber : null),
                 };
        if (ids is not null) mq = mq.Where(x => ids.Contains(x.CourtId));
        var mrows = await mq.ToListAsync(ct);
        var miscs = mrows
            .GroupBy(r => Room.ScopeKey(r.NumberingPolicy, r.CourtId, r.RoomId, r.NumberingLevel))
            .Select(g =>
            {
                var cand = g.OrderByDescending(x => x.Misc).First();
                return new DeletableMiscDto(g.Key, cand.CourtId, cand.CourtName,
                    ScopeLabel(cand.NumberingPolicy, cand.RoomName, cand.NumberingLevel),
                    cand.Id, cand.Misc, cand.OriginalCopyNumber, cand.ReferenceNumber, cand.State);
            })
            .OrderBy(s => s.CourtName).ThenBy(s => s.ScopeLabel).ToList();

        return new DeletionTargetsDto(normals, miscs);
    }

    public async Task<IReadOnlyList<OriginalCopyOption>> ListSelectableOriginalsAsync(
        IReadOnlyCollection<Guid>? courtIds, CancellationToken ct)
    {
        var ids = courtIds?.ToArray();
        var q = from cr in db.CopyRequests.AsNoTracking()
                join c in db.Courts on cr.CourtId equals c.Id
                where cr.Category == CaseCategory.Normal && cr.State == CopyState.Approved && cr.CopyNumber != null
                select new { cr.Id, CopyNumber = cr.CopyNumber!, cr.CourtId, CourtName = c.Name, cr.CaseBaseNumber, cr.ReservationDate };
        if (ids is not null) q = q.Where(x => ids.Contains(x.CourtId));
        return await q.OrderBy(x => x.CourtName).ThenBy(x => x.CopyNumber)
            .Select(x => new OriginalCopyOption(x.Id, x.CopyNumber, x.CourtId, x.CourtName, x.CaseBaseNumber, x.ReservationDate))
            .ToListAsync(ct);
    }

    private static string ScopeLabel(NumberingPolicy p, string roomName, string? level) => p switch
    {
        NumberingPolicy.Room => $"مستوى الغرفة: {roomName}",
        NumberingPolicy.Special => $"مستوى خاص: {level}",
        _ => "مستوى المحكمة",
    };

    public async Task<IReadOnlyList<CopyNumberCounterDto>> ListCopyNumberCountersAsync(CancellationToken ct) =>
        await (from cc in db.CourtCopyCounters.AsNoTracking()
               join c in db.Courts on cc.CourtId equals c.Id
               join rm in db.Rooms on cc.RoomId equals rm.Id into rmj
               from room in rmj.DefaultIfEmpty()
               orderby c.Code, cc.RoomId, cc.Year
               // RoomId == empty → court-wide sequence; otherwise the room's own sequence.
               select new CopyNumberCounterDto(
                   cc.CourtId, c.Code, c.Name,
                   cc.RoomId == Guid.Empty ? (Guid?)null : cc.RoomId,
                   cc.RoomId == Guid.Empty ? "مستوى المحكمة" : (room != null ? room.Name : "?"),
                   cc.Year, cc.LastNumber)).ToListAsync(ct);

    public async Task<IReadOnlyList<MiscNumberCounterDto>> ListMiscNumberCountersAsync(CancellationToken ct)
    {
        var counters = await db.MiscNumberCounters.AsNoTracking().ToListAsync(ct);
        if (counters.Count == 0) return [];
        var courts = await db.Courts.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => new { r.Name, r.CourtId }, ct);

        var list = new List<MiscNumberCounterDto>();
        foreach (var k in counters)
        {
            var parts = k.ScopeKey.Split(':');
            var courtId = Guid.Empty;
            var label = k.ScopeKey;
            if (parts[0] == "C" && Guid.TryParse(parts[1], out var cc)) { courtId = cc; label = "مستوى المحكمة"; }
            else if (parts[0] == "R" && Guid.TryParse(parts[1], out var rid))
            { var rm = rooms.GetValueOrDefault(rid); courtId = rm?.CourtId ?? Guid.Empty; label = $"مستوى الغرفة: {rm?.Name ?? parts[1]}"; }
            else if (parts[0] == "S" && Guid.TryParse(parts[1], out var cc2))
            { courtId = cc2; label = $"مستوى خاص: {(parts.Length > 2 ? parts[2] : "")}"; }

            var courtName = courtId != Guid.Empty ? courts.GetValueOrDefault(courtId) ?? "" : "";
            list.Add(new MiscNumberCounterDto(k.ScopeKey, courtId, courtName, label, k.Year, k.LastNumber));
        }
        return list.OrderBy(x => x.CourtName).ThenBy(x => x.ScopeLabel).ThenBy(x => x.Year).ToList();
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(Guid copyRequestId, CancellationToken ct) =>
        await db.AuditEntries.AsNoTracking()
            .Where(a => a.CopyRequestId == copyRequestId)
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => new AuditEntryDto(a.ActorName, a.Action, a.TimestampUtc, a.Reason, a.BeforeJson, a.AfterJson))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CourtDto>> ListCourtsAsync(IReadOnlyCollection<Guid>? restrictTo, bool activeOnly, CancellationToken ct)
    {
        var ids = restrictTo?.ToArray();
        var q = db.Courts.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(c => c.IsActive);
        // null => all courts; a non-null list restricts (empty => none).
        if (ids is not null) q = q.Where(c => ids.Contains(c.Id));
        return await q.OrderBy(c => c.Name)
            .Select(c => new CourtDto(c.Id, c.Code, c.Name, c.IsActive)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? courtId, bool activeOnly, CancellationToken ct)
    {
        var q = db.Rooms.AsNoTracking().AsQueryable();
        if (courtId is { } cid) q = q.Where(r => r.CourtId == cid);
        if (activeOnly) q = q.Where(r => r.IsActive);
        return await q.OrderBy(r => r.Code).ThenBy(r => r.Name)
            .Select(r => new RoomDto(r.Id, r.CourtId, r.Code, r.Name, r.IsActive, r.NumberingPolicy, r.NumberingLevel, r.CopyNumberingPolicy)).ToListAsync(ct);
    }

    public async Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.AsNoTracking().Where(r => r.Id == roomId)
            .Select(r => new RoomDto(r.Id, r.CourtId, r.Code, r.Name, r.IsActive, r.NumberingPolicy, r.NumberingLevel, r.CopyNumberingPolicy)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ListUsersByRoleAndCourtAsync(Role role, Guid courtId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == role && u.IsActive
                        && db.Set<UserCourt>().Any(uc => uc.UserId == u.Id && uc.CourtId == courtId))
            .OrderBy(u => u.DisplayName)
            .Select(u => new LookupItem(u.Id, u.DisplayName)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ListJudgesByRoomAsync(Guid roomId, CancellationToken ct) =>
        await db.Judges.AsNoTracking()
            .Where(j => j.IsActive
                        && db.Set<JudgeRoom>().Any(jr => jr.JudgeId == j.Id && jr.RoomId == roomId))
            .OrderBy(j => j.Name)
            .Select(j => new LookupItem(j.Id, j.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ListActiveJudgesAsync(CancellationToken ct) =>
        await db.Judges.AsNoTracking()
            .Where(j => j.IsActive)
            .OrderBy(j => j.Name)
            .Select(j => new LookupItem(j.Id, j.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupItem>> ListPanelMemberTitlesAsync(CancellationToken ct) =>
        await db.PanelMemberTitles.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new LookupItem(t.Id, t.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<ParagraphTemplateDto>> ListParagraphTemplatesAsync(
        bool includeArchived, Guid? formTemplateId, bool onlyForTemplate, CancellationToken ct)
    {
        var q = db.ParagraphTemplates.AsNoTracking().AsQueryable();
        if (!includeArchived) q = q.Where(p => !p.IsArchived);
        // For insertion: paragraphs belonging to the form type, plus global (null) ones.
        if (onlyForTemplate)
            q = q.Where(p => p.FormTemplateId == formTemplateId || p.FormTemplateId == null);
        return await q.OrderBy(p => p.Title)
            .Select(p => new ParagraphTemplateDto(p.Id, p.Title, p.Body, p.IsArchived, p.FormTemplateId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FormTemplateDto>> ListFormTemplatesAsync(bool activeOnly, CancellationToken ct)
    {
        var q = db.FormTemplates.AsNoTracking().Include(f => f.Fields).AsQueryable();
        if (activeOnly) q = q.Where(f => f.IsActive);
        return await q.OrderBy(f => f.Name)
            .Select(f => new FormTemplateDto(f.Id, f.Name, f.IsActive,
                f.Fields.OrderBy(x => x.Order)
                    .Select(x => new FormFieldDto(x.Id, x.Key, x.Label, x.Type, x.ValidationRulesJson, x.Order)).ToList()))
            .ToListAsync(ct);
    }
}
