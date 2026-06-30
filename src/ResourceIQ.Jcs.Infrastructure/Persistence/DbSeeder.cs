using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT-ONLY seed data so the workflow is demoable end-to-end. Each part is idempotent.
/// Never run in production — the passwords here are throwaway dev credentials.
/// </summary>
public static class DbSeeder
{
    public const string DevPassword = "Pass@word1";

    /// <summary>Legacy single template, superseded by the per-decision-type templates below.</summary>
    private const string LegacyTemplateName = "إعلام الحكم – محكمة النقض";

    /// <summary>Full DEV seed: reference data (courts/rooms/judges/schedule/templates) then demo
    /// users. Reference data is seeded FIRST so the demo users can be scoped to the real courts.</summary>
    public static async Task SeedAsync(JcsDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await SeedReferenceDataAsync(db, ct);
        await SeedUsersAsync(db, hasher, ct);
    }

    /// <summary>
    /// Reference/config data only (no demo users): the decision-type templates and their
    /// insertable paragraphs. Safe and idempotent — used by the production bootstrap too.
    /// </summary>
    public static async Task SeedReferenceDataAsync(JcsDbContext db, CancellationToken ct = default)
    {
        await DeactivateLegacyTemplateAsync(db, ct);
        await SeedDecisionTypesAsync(db, ct);
        await SeedCourtsAndRoomsAsync(db, ct);
        await SeedJudgesAsync(db, ct);
        await SeedPanelMemberTitlesAsync(db, ct);
        await SeedScheduleAssignmentsAsync(db, ct);
    }

    /// <summary>
    /// Seeds the official courts (محاكم) and their rooms (غرف) from <see cref="CourtRoomSeedData"/>.
    /// Idempotent: a court is created once (matched by code), and within it each room is created once
    /// (matched by code). Existing data is never modified or removed.
    /// </summary>
    private static async Task SeedCourtsAndRoomsAsync(JcsDbContext db, CancellationToken ct)
    {
        var changed = false;
        foreach (var (courtCode, courtName, rooms) in CourtRoomSeedData.Courts)
        {
            var court = await db.Courts.FirstOrDefaultAsync(c => c.Code == courtCode, ct);
            if (court is null)
            {
                court = new Court { Code = courtCode, Name = courtName, IsActive = true };
                db.Courts.Add(court);
                await db.SaveChangesAsync(ct); // need the court Id before adding its rooms
            }

            var existingRoomCodes = (await db.Rooms.Where(r => r.CourtId == court.Id).Select(r => r.Code).ToListAsync(ct))
                .ToHashSet();

            // Default رقم المتفرق policy (preserves prior behaviour): جزائية (code "2") numbers
            // per-room; all other courts number per-court. Admins can change it later (FR-06).
            var policy = courtCode == "2" ? NumberingPolicy.Room : NumberingPolicy.Court;
            foreach (var (roomCode, roomName) in rooms)
            {
                if (!existingRoomCodes.Add(roomCode)) continue; // already present
                db.Rooms.Add(new Room { CourtId = court.Id, Code = roomCode, Name = roomName, IsActive = true, NumberingPolicy = policy });
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the Court of Cassation judges roster (JudgeSeedData). Idempotent — judges whose name
    /// already exists are skipped. The محكمة النقض court and a default room (غرفة) within it are
    /// created if missing; each new judge is assigned to that room so they appear in the
    /// judging-panel pickers (judges belong to rooms, not directly to courts).
    /// </summary>
    /// <summary>
    /// Ensures the full judges roster (<see cref="JudgeSeedData"/>) exists. Room assignments are
    /// applied separately by <see cref="SeedScheduleAssignmentsAsync"/>. Idempotent (by name).
    /// </summary>
    private static async Task SeedJudgesAsync(JcsDbContext db, CancellationToken ct)
    {
        var existing = (await db.Judges.Select(j => j.Name).ToListAsync(ct))
            .Select(n => n.Trim()).ToHashSet();

        var added = 0;
        foreach (var raw in JudgeSeedData.Names)
        {
            var name = raw.Trim();
            if (name.Length == 0 || !existing.Add(name)) continue; // skip blanks + duplicates
            db.Judges.Add(new Judge { Name = name, IsActive = true });
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the default judging-panel member titles (صفات): رئيس الهيئة، نائب الرئيس، عضو، مستشار.
    /// Idempotent — a title whose name already exists is skipped. Admins can add/edit/deactivate
    /// them later from the Panel-titles screen; the copyist picks one per panel member while editing.
    /// </summary>
    private static async Task SeedPanelMemberTitlesAsync(JcsDbContext db, CancellationToken ct)
    {
        var defaults = new[] { "رئيس الهيئة", "نائب الرئيس", "عضو", "مستشار" };
        var existing = (await db.PanelMemberTitles.Select(t => t.Name).ToListAsync(ct))
            .Select(n => n.Trim()).ToHashSet();

        var order = 0;
        var added = false;
        foreach (var name in defaults)
        {
            order++;
            if (!existing.Add(name)) continue;
            db.PanelMemberTitles.Add(new PanelMemberTitle { Name = name, DisplayOrder = order, IsActive = true });
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the monthly schedule (<see cref="ScheduleSeedData"/>): each chamber becomes a room
    /// (matched by name within its court — reusing an official room if the name matches exactly,
    /// otherwise created verbatim with an "S#" code), and every listed judge is linked to it.
    /// Judges are matched to the roster by a normalized name (alef/yaa/ta-marbuta/spaces) and
    /// created if absent. Idempotent: existing rooms/links are reused, never duplicated.
    /// </summary>
    private static async Task SeedScheduleAssignmentsAsync(JcsDbContext db, CancellationToken ct)
    {
        var byNorm = new Dictionary<string, Judge>();
        foreach (var j in await db.Judges.ToListAsync(ct)) byNorm.TryAdd(NormalizeName(j.Name), j);

        var roomCount = new Dictionary<Guid, int>(); // per-court running count for "S#" codes

        foreach (var (courtCode, roomName, judgeNames) in ScheduleSeedData.Rooms)
        {
            var court = await db.Courts.FirstOrDefaultAsync(c => c.Code == courtCode, ct);
            if (court is null) continue; // courts are seeded earlier

            var room = await db.Rooms.FirstOrDefaultAsync(r => r.CourtId == court.Id && r.Name == roomName, ct);
            if (room is null)
            {
                if (!roomCount.TryGetValue(court.Id, out var rc))
                    rc = await db.Rooms.CountAsync(r => r.CourtId == court.Id, ct);
                rc++;
                roomCount[court.Id] = rc;
                var policy = courtCode == "2" ? NumberingPolicy.Room : NumberingPolicy.Court; // جزائية → per-room
                room = new Room { CourtId = court.Id, Code = $"S{rc}", Name = roomName, IsActive = true, NumberingPolicy = policy };
                db.Rooms.Add(room);
            }

            var linked = (await db.Set<JudgeRoom>().Where(jr => jr.RoomId == room.Id)
                .Select(jr => jr.JudgeId).ToListAsync(ct)).ToHashSet();

            foreach (var rawName in judgeNames)
            {
                var name = rawName.Trim();
                if (name.Length == 0) continue;

                var norm = NormalizeName(name);
                if (!byNorm.TryGetValue(norm, out var judge))
                {
                    judge = new Judge { Name = name, IsActive = true };
                    db.Judges.Add(judge);
                    byNorm[norm] = judge;
                }
                if (linked.Add(judge.Id))
                    db.Set<JudgeRoom>().Add(new JudgeRoom { JudgeId = judge.Id, RoomId = room.Id });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Conservative Arabic name normalization for matching (does NOT strip "ال").</summary>
    private static string NormalizeName(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Trim())
        {
            var c = ch switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ة' => 'ه', 'ؤ' => 'و', 'ئ' => 'ي', _ => ch };
            if (c is 'ـ' or ' ') continue; // drop tatweel + spaces
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static async Task SeedUsersAsync(JcsDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        // Scope the demo workflow users to every seeded official court (BR-06) so the end-to-end
        // demo can act on the real courts/rooms/judges. Reference data is seeded before this runs.
        var courtIds = await db.Courts.Select(c => c.Id).ToListAsync(ct);

        User Make(string username, string display, Role role) => new()
        {
            Username = username,
            DisplayName = display,
            Role = role,
            IsActive = true,
            PasswordHash = hasher.Hash(DevPassword),
        };

        var admin = Make("admin", "مدير النظام", Role.Administrator);
        var head = Make("head", "رئيس الديوان", Role.RegistryHead);
        var copyist = Make("copyist", "الناسخ", Role.Copyist);
        var reviewer = Make("reviewer", "المدقق", Role.Reviewer);
        db.Users.AddRange(admin, head, copyist, reviewer);

        // Admin is not court-scoped (manages everything); the workflow roles see all seeded courts.
        foreach (var u in new[] { head, copyist, reviewer })
            foreach (var cid in courtIds)
                u.Courts.Add(new UserCourt { UserId = u.Id, CourtId = cid });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hide the old single template (and its paragraphs) once typed templates exist.</summary>
    private static async Task DeactivateLegacyTemplateAsync(JcsDbContext db, CancellationToken ct)
    {
        var legacy = await db.FormTemplates.FirstOrDefaultAsync(t => t.Name == LegacyTemplateName, ct);
        if (legacy is null || !legacy.IsActive) return;

        legacy.IsActive = false;
        await db.ParagraphTemplates.Where(p => p.FormTemplateId == legacy.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, true), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds one FormTemplate per decision type (نوع القرار). All share the same FIXED fields
    /// (judging panel + dates); each carries its own set of insertable paragraphs. Idempotent:
    /// a type's template is created once and its paragraphs seeded once.
    /// </summary>
    private static async Task SeedDecisionTypesAsync(JcsDbContext db, CancellationToken ct)
    {
        foreach (var (type, paragraphs) in DecisionTypes)
        {
            // Re-sync the FIXED fields each run so structural changes (e.g. the dynamic panel
            // members field) propagate to the seeded decision-type templates.
            var t = await db.FormTemplates.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Name == type, ct);
            if (t is null)
            {
                t = new FormTemplate { Name = type, IsActive = true };
                AddFixedFields(t);
                db.FormTemplates.Add(t);
            }
            else
            {
                db.FormFields.RemoveRange(t.Fields);
                t.Fields.Clear();
                AddFixedFields(t);
                t.IsActive = true;
            }
            await db.SaveChangesAsync(ct);

            if (!await db.ParagraphTemplates.AnyAsync(p => p.FormTemplateId == t.Id, ct))
            {
                foreach (var title in paragraphs)
                    db.ParagraphTemplates.Add(new ParagraphTemplate
                    {
                        Title = title, Body = string.Empty, FormTemplateId = t.Id, IsArchived = false,
                    });
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // FIXED fields shared by every decision type: the judging panel ("الهيئة الحاكمة") + the
    // issue-date line ("قراراً صدر في"). type "judge" → picked from the court's judges (no
    // repeats); "date" → date picker. Everything else is built by inserting paragraphs.
    private static void AddFixedFields(FormTemplate t)
    {
        void F(string key, string label, string type, int order) =>
            t.Fields.Add(new FormField { FormTemplateId = t.Id, Key = key, Label = label, Type = type, Order = order });

        F("chamber",        "الهيئة الحاكمة",              "text",   1);
        F("president",      "رئيس الهيئة",                 "judge",  2);
        // Panel members: a president plus AT LEAST TWO members (مستشارون). Dynamic list.
        F("members",        "أعضاء الهيئة (مستشارون)",     "judges", 3);
        F("decisionNumber", "رقم القرار",                 "text",   4);
        F("year",           "السنة",                      "text",   5);
        F("issueHijri",     "تاريخ الإصدار (هجري)",        "text",   6);
        F("issueGregorian", "تاريخ الإصدار (ميلادي)",      "date",   7);
    }

    // Decision types and their insertable paragraphs (per the registry's catalogue). Obvious
    // typos in the source were normalized; admins can rename/add via the Paragraphs screen.
    private static readonly (string Type, string[] Paragraphs)[] DecisionTypes =
    [
        ("عادي",
        [
            "مقدمة", "بقرار ندب", "محكمة النقض - الدائرة الجزائية - الغرفة",
            "المطلوب التجديد بمواجهته", "الطاعن", "المدعي طالب الطعن", "طالب التجديد",
            "المطلوب الطعن بمواجهته والمطلوب التجديد", "الحكم موضوع التجديد",
            "المطعون ضده", "المدعى عليهم المطعون ضدهم", "الجرم",
            "القرار المطعون فيه", "القرار موضوع الطعن", "في الوقائع", "في الموضوع",
            "أسباب الطعن", "في الشكل", "النظر في الطلب",
            "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة", "القرار موضوع المخاصمة",
            "النظر في الطعن", "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "وفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("انعدام",
        [
            "طالب الانعدام", "المطلوب الانعدام ضده", "طلب انعدام قرار", "المدعي طالب الطعن",
            "القرار المطلوب انعدامه", "طلب انعدام القرار", "القرار موضوع الطعن", "أسباب الانعدام",
            "في الشكل", "النظر في الطلب",
            "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة", "القرار موضوع المخاصمة",
            "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("إعادة",
        [
            "المدعي طالب الطعن", "طالب إعادة المحاكمة", "المطلوب الإعادة ضده",
            "المدعى عليهم المطعون ضدهم", "القرار المطلوب الإعادة فيه", "القرار موضوع الطعن",
            "في الشكل", "النظر في الطلب", "أسباب الإعادة",
            "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة", "القرار موضوع المخاصمة",
            "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("اعتراض",
        [
            "المدعي طالب الطعن", "المعترض عليه", "المدعى عليهم المطعون ضدهم", "المعترض",
            "القرار المعترض عليه", "القرار موضوع الطعن", "في الشكل", "النظر في الطلب",
            "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة", "القرار موضوع المخاصمة",
            "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("تصحيح",
        [
            "المدعي طالب الطعن", "طالب التصحيح", "المطلوب التصحيح ضده",
            "المدعى عليهم المطعون ضدهم", "القرار المطلوب تصحيحه", "أسباب الطلب",
            "القرار موضوع الطعن", "في الشكل", "النظر في الطلب",
            "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة", "القرار موضوع المخاصمة",
            "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("تفسير",
        [
            "المدعي طالب الطعن", "المطلوب التفسير ضده", "المدعى عليهم المطعون ضدهم",
            "طالب التفسير", "القرار المطلوب تفسيره", "القرار موضوع الطعن", "في الشكل",
            "النظر في الطلب", "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة",
            "القرار موضوع المخاصمة", "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("طلب تعيين المرجع",
        [
            "المدعي طالب الطعن", "طالب تعيين المرجع", "المدعى عليهم المطعون ضدهم",
            "المطلوب تعيين المرجع بمواجهته", "موضوع تعيين المرجع", "أسباب طلب تعيين المرجع",
            "تعيين المرجع", "في النظر بطلب تعيين المرجع", "القرار موضوع الطعن", "في الشكل",
            "النظر في الطلب", "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة",
            "القرار موضوع المخاصمة", "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("نقل الدعوى",
        [
            "المدعي طالب الطعن", "طالب النقل", "المدعى عليهم المطعون ضدهم",
            "المطلوب النقل بمواجهته", "القرار المطعون فيه", "موضوع طلب النقل", "أسباب طلب النقل",
            "النظر في طلب النقل", "القرار موضوع الطعن", "في الشكل", "النظر في الطلب",
            "الجهة المدعية بالمخاصمة", "القرار موضوع المخاصمة", "النظر في الدعوى",
            "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
        ("شطب",
        [
            "المدعي طالب الطعن", "المدعى عليهم المطعون ضدهم", "القرار موضوع الطعن", "في الشكل",
            "النظر في الطلب", "الجهة المدعية بالمخاصمة", "الجهة المدعى عليها بالمخاصمة",
            "القرار موضوع المخاصمة", "النظر في الدعوى", "أسباب المخاصمة", "في القانون",
            "ووفقاً لمطالبة النيابة العامة", "وخلافاً لمطالبة النيابة العامة",
            "ووفقاً لمطالبة النيابة العامة من جهة وخلافاً", "وعملاً بأحكام القرار",
            "تقرر بالاتفاق", "تقرر بالإجماع", "تقرر بالأكثرية", "المخالفة", "الرد على المخالفة",
        ]),
    ];
}
