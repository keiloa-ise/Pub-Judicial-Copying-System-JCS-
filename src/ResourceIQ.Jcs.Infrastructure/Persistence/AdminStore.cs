using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Application.Common;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Domain.Rules;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>EF implementation of administrator-managed data. Each method saves atomically.</summary>
public sealed class AdminStore(JcsDbContext db) : IAdminStore
{
    // ── Courts ──
    public Task<bool> CourtCodeExistsAsync(string code, CancellationToken ct) =>
        db.Courts.AnyAsync(c => c.Code == code, ct);

    public async Task<Guid> CreateCourtAsync(string code, string name, CancellationToken ct)
    {
        if (await db.Courts.AnyAsync(c => c.Name == name, ct))
            throw new DomainException("اسم المحكمة مستخدم مسبقاً.");
        var court = new Court { Code = code, Name = name, IsActive = true };
        db.Courts.Add(court);
        await db.SaveChangesAsync(ct);
        return court.Id;
    }

    public async Task UpdateCourtAsync(Guid id, string name, bool isActive, CancellationToken ct)
    {
        var court = await db.Courts.FindAsync([id], ct) ?? throw new NotFoundException("Court not found.");
        if (await db.Courts.AnyAsync(c => c.Name == name && c.Id != id, ct))
            throw new DomainException("اسم المحكمة مستخدم مسبقاً.");
        court.Name = name;
        court.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    // ── Rooms ──
    public Task<bool> CourtExistsAsync(Guid courtId, CancellationToken ct) =>
        db.Courts.AnyAsync(c => c.Id == courtId, ct);

    public Task<bool> RoomCodeExistsInCourtAsync(Guid courtId, string code, CancellationToken ct) =>
        db.Rooms.AnyAsync(r => r.CourtId == courtId && r.Code == code, ct);

    public async Task<Guid> CreateRoomAsync(Guid courtId, string code, string name, NumberingPolicy policy, string? level, CancellationToken ct)
    {
        if (await db.Rooms.AnyAsync(r => r.CourtId == courtId && r.Name == name, ct))
            throw new DomainException("اسم الغرفة مستخدم مسبقاً في هذه المحكمة.");
        var room = new Room { CourtId = courtId, Code = code, Name = name, IsActive = true, NumberingPolicy = policy, NumberingLevel = level };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return room.Id;
    }

    public async Task UpdateRoomAsync(Guid id, string name, bool isActive, NumberingPolicy policy, string? level, CancellationToken ct)
    {
        var room = await db.Rooms.FindAsync([id], ct) ?? throw new NotFoundException("Room not found.");
        if (await db.Rooms.AnyAsync(r => r.CourtId == room.CourtId && r.Name == name && r.Id != id, ct))
            throw new DomainException("اسم الغرفة مستخدم مسبقاً في هذه المحكمة.");
        room.Name = name;
        room.IsActive = isActive;
        room.NumberingPolicy = policy;
        room.NumberingLevel = level;
        await db.SaveChangesAsync(ct);
    }

    // ── Numbering start points (FR-17) ──
    public async Task SetCopyNumberStartAsync(Guid courtId, int year, int lastNumber, CancellationToken ct)
    {
        var nums = await db.CopyRequests
            .Where(cr => cr.CourtId == courtId && cr.ReservationDate.Year == year && cr.CopyNumber != null)
            .Select(cr => cr.CopyNumber!).ToListAsync(ct);
        var maxUsed = nums.Select(ParseSeq).DefaultIfEmpty(0).Max();
        if (lastNumber < maxUsed)
            throw new DomainException($"لا يمكن ضبط البداية ({lastNumber}) أقل من أعلى رقم نسخة مُستخدَم فعلاً ({maxUsed}).");

        var c = await db.CourtCopyCounters.FindAsync([courtId, year], ct);
        if (c is null) db.CourtCopyCounters.Add(new CourtCopyCounter { CourtId = courtId, Year = year, LastNumber = lastNumber });
        else c.LastNumber = lastNumber;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetMiscNumberStartAsync(Guid courtId, string scopeKey, int year, int lastNumber, CancellationToken ct)
    {
        var rows = await (from cr in db.CopyRequests
                          join rm in db.Rooms on cr.RoomId equals rm.Id
                          where cr.CourtId == courtId && cr.ReservationDate.Year == year
                                && cr.Category == CaseCategory.Miscellaneous && cr.MiscNumber != null
                          select new { Misc = cr.MiscNumber!.Value, rm.NumberingPolicy, rm.NumberingLevel, rm.Id, rm.CourtId })
                         .ToListAsync(ct);
        var maxUsed = rows
            .Where(r => Room.ScopeKey(r.NumberingPolicy, r.CourtId, r.Id, r.NumberingLevel) == scopeKey)
            .Select(r => r.Misc).DefaultIfEmpty(0).Max();
        if (lastNumber < maxUsed)
            throw new DomainException($"لا يمكن ضبط البداية ({lastNumber}) أقل من أعلى رقم متفرق مُستخدَم فعلاً ({maxUsed}).");

        var k = await db.MiscNumberCounters.FindAsync([scopeKey, year], ct);
        if (k is null) db.MiscNumberCounters.Add(new MiscNumberCounter { ScopeKey = scopeKey, Year = year, LastNumber = lastNumber });
        else k.LastNumber = lastNumber;
        await db.SaveChangesAsync(ct);
    }

    private static int ParseSeq(string copyNumber)
    {
        var p = copyNumber.Split('/');
        return p.Length == 3 && int.TryParse(p[2], out var s) ? s : 0;
    }

    // ── Users ──
    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Role, u.IsActive,
                u.Courts.Select(c => c.CourtId).ToList()))
            .ToListAsync(ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Username == username, ct);

    public async Task<Guid> CreateUserAsync(string username, string displayName, Role role, string passwordHash,
        IReadOnlyCollection<Guid> courtIds, CancellationToken ct)
    {
        var user = new User
        {
            Username = username, DisplayName = displayName, Role = role,
            PasswordHash = passwordHash, IsActive = true,
        };
        foreach (var cid in courtIds.Distinct())
            user.Courts.Add(new UserCourt { UserId = user.Id, CourtId = cid });

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task UpdateUserAsync(Guid id, string displayName, Role role, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct) ?? throw new NotFoundException("User not found.");
        user.DisplayName = displayName;
        user.Role = role;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetUserActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct) ?? throw new NotFoundException("User not found.");
        user.IsActive = active;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPasswordHashAsync(Guid id, string passwordHash, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct) ?? throw new NotFoundException("User not found.");
        user.PasswordHash = passwordHash;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetUserCourtsAsync(Guid id, IReadOnlyCollection<Guid> courtIds, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Courts).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User not found.");
        user.Courts.Clear();
        foreach (var cid in courtIds.Distinct())
            user.Courts.Add(new UserCourt { UserId = user.Id, CourtId = cid });
        await db.SaveChangesAsync(ct);
    }

    // ── Judges ──
    public async Task<IReadOnlyList<JudgeDto>> ListJudgesAsync(CancellationToken ct) =>
        await db.Judges.AsNoTracking().OrderBy(j => j.Name)
            .Select(j => new JudgeDto(j.Id, j.Name, j.IsActive, j.Rooms.Select(r => r.RoomId).ToList()))
            .ToListAsync(ct);

    public async Task<bool> RoomsExistAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        var ids = roomIds.Distinct().ToArray();
        return await db.Rooms.CountAsync(r => ids.Contains(r.Id), ct) == ids.Length;
    }

    public async Task<Guid> CreateJudgeAsync(string name, IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        if (await db.Judges.AnyAsync(j => j.Name == name, ct))
            throw new DomainException("اسم القاضي مستخدم مسبقاً.");
        var judge = new Judge { Name = name, IsActive = true };
        foreach (var rid in roomIds.Distinct())
            judge.Rooms.Add(new JudgeRoom { JudgeId = judge.Id, RoomId = rid });
        db.Judges.Add(judge);
        await db.SaveChangesAsync(ct);
        return judge.Id;
    }

    public async Task UpdateJudgeAsync(Guid id, string name, bool isActive, IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        var judge = await db.Judges.Include(j => j.Rooms).FirstOrDefaultAsync(j => j.Id == id, ct)
                    ?? throw new NotFoundException("Judge not found.");
        if (await db.Judges.AnyAsync(j => j.Name == name && j.Id != id, ct))
            throw new DomainException("اسم القاضي مستخدم مسبقاً.");
        judge.Name = name;
        judge.IsActive = isActive;
        judge.Rooms.Clear();
        foreach (var rid in roomIds.Distinct())
            judge.Rooms.Add(new JudgeRoom { JudgeId = judge.Id, RoomId = rid });
        await db.SaveChangesAsync(ct);
    }

    // ── Panel-member titles (صفات) ──
    public async Task<IReadOnlyList<PanelMemberTitleDto>> ListPanelMemberTitlesAsync(CancellationToken ct) =>
        await db.PanelMemberTitles.AsNoTracking().OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new PanelMemberTitleDto(t.Id, t.Name, t.IsActive, t.DisplayOrder))
            .ToListAsync(ct);

    public async Task<Guid> CreatePanelMemberTitleAsync(string name, int displayOrder, CancellationToken ct)
    {
        if (await db.PanelMemberTitles.AnyAsync(t => t.Name == name, ct))
            throw new DomainException("صفة العضو مستخدمة مسبقاً.");
        var title = new PanelMemberTitle { Name = name, DisplayOrder = displayOrder, IsActive = true };
        db.PanelMemberTitles.Add(title);
        await db.SaveChangesAsync(ct);
        return title.Id;
    }

    public async Task UpdatePanelMemberTitleAsync(Guid id, string name, bool isActive, int displayOrder, CancellationToken ct)
    {
        var title = await db.PanelMemberTitles.FindAsync([id], ct) ?? throw new NotFoundException("Panel-member title not found.");
        if (await db.PanelMemberTitles.AnyAsync(t => t.Name == name && t.Id != id, ct))
            throw new DomainException("صفة العضو مستخدمة مسبقاً.");
        title.Name = name;
        title.IsActive = isActive;
        title.DisplayOrder = displayOrder;
        await db.SaveChangesAsync(ct);
    }

    // ── Paragraph templates ──
    public async Task<Guid> CreateParagraphAsync(string title, string body, Guid? formTemplateId, CancellationToken ct)
    {
        var p = new ParagraphTemplate { Title = title, Body = body, FormTemplateId = formTemplateId, IsArchived = false };
        db.ParagraphTemplates.Add(p);
        await db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdateParagraphAsync(Guid id, string title, string body, bool isArchived, Guid? formTemplateId, CancellationToken ct)
    {
        var p = await db.ParagraphTemplates.FindAsync([id], ct) ?? throw new NotFoundException("Paragraph not found.");
        p.Title = title;
        p.Body = body;
        p.IsArchived = isArchived;
        p.FormTemplateId = formTemplateId;
        await db.SaveChangesAsync(ct);
    }

    // ── Form templates ──
    public async Task<Guid> CreateFormTemplateAsync(string name, IReadOnlyCollection<NewFormField> fields, CancellationToken ct)
    {
        var template = new FormTemplate { Name = name, IsActive = true };
        foreach (var f in fields)
            template.Fields.Add(new FormField
            {
                FormTemplateId = template.Id, Key = f.Key, Label = f.Label,
                Type = f.Type, ValidationRulesJson = f.ValidationRulesJson, Order = f.Order,
            });
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template.Id;
    }

    public async Task UpdateFormTemplateAsync(Guid id, string name, bool isActive, IReadOnlyCollection<NewFormField> fields, CancellationToken ct)
    {
        var template = await db.FormTemplates.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == id, ct)
                       ?? throw new NotFoundException("Form template not found.");
        template.Name = name;
        template.IsActive = isActive;
        db.FormFields.RemoveRange(template.Fields);
        template.Fields.Clear();
        foreach (var f in fields)
            template.Fields.Add(new FormField
            {
                FormTemplateId = template.Id, Key = f.Key, Label = f.Label,
                Type = f.Type, ValidationRulesJson = f.ValidationRulesJson, Order = f.Order,
            });
        await db.SaveChangesAsync(ct);
    }
}
