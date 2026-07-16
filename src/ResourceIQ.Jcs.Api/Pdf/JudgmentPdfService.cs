using System.Reflection;
using System.Text.Json;
using ClosedXML;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResourceIQ.Jcs.Application.ReadModels;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Api.Pdf;

/// <summary>
/// FR-15: renders the official "إعلام الحكم" as a server-generated PDF, straight from the
/// authoritative <see cref="CopyRequestDetail"/>. Because the bytes are produced on the server,
/// the user cannot edit the document (e.g. via browser dev-tools) before printing — closing the
/// pre-print tampering vector of the old client-rendered page.
///
/// Mirrors the on-screen layout: judging panel + inserted sections, رقم القرار centred, NO case
/// base number, and a repeated "مسودة قرار" watermark on every page while the copy is not Approved.
/// Arabic is rendered with the embedded Amiri font (no reliance on system fonts → works on Linux).
/// </summary>
public sealed class JudgmentPdfService
{
    private const string Font = "Amiri";
    private static readonly byte[] LogoFaint;

    static JudgmentPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false; // only the embedded font — deterministic on Linux

        var asm = typeof(JudgmentPdfService).Assembly;
        using (var reg = Resource(asm, "ResourceIQ.Jcs.Api.Assets.Fonts.Amiri-Regular.ttf")) FontManager.RegisterFont(reg);
        using (var bold = Resource(asm, "ResourceIQ.Jcs.Api.Assets.Fonts.Amiri-Bold.ttf")) FontManager.RegisterFont(bold);
        LogoFaint = ReadAll(asm, "ResourceIQ.Jcs.Api.Assets.logo-faint.png");
    }

    public byte[] Render(CopyRequestDetail d)
    {
        var fields = ParseFields(d.FieldValuesJson);
        string G(string key) => fields.TryGetValue(key, out var v) ? v.Trim() : "";

        var members = ParsePanelMembers(G("members"));
        var sections = ParseSections(d.SectionsJson);
        var dissentSections = ParseSections(d.DissentSectionsJson);
        var rebuttalSections = ParseSections(d.RebuttalSectionsJson);

        // رقم النسخة is {court}/{year}/{seq} or {court}/{room}/{year}/{seq}: court is first, year is
        // second-to-last, seq is last. Handles both the court-level and room-level formats (FR-03).
        var parts = (d.CopyNumber ?? "").Split('/');
        var courtCode = parts.Length >= 3 ? parts[0] : "";
        var year = parts.Length >= 3 ? parts[^2] : G("year");
        var draft = d.State != CopyState.Approved;

        var qrLines = new List<string>();
        if (d.MiscNumber is { } ms)
        {
            qrLines.Add($"رقم المتفرق: {ms}");
            if (!string.IsNullOrWhiteSpace(d.OriginalCopyNumber)) qrLines.Add($"مستند إلى النسخة: {d.OriginalCopyNumber}");
        }
        else qrLines.Add($"رقم النسخة: {d.CopyNumber ?? "—"}");
        qrLines.Add($"المحكمة: {d.CourtName}{(courtCode.Length > 0 ? $" ({courtCode})" : "")}");
        qrLines.Add($"السنة: {(year.Length > 0 ? year : "—")}");
        var qr = QrPng(string.Join("\n", qrLines));

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(16, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(12).LineHeight(1.5f).DirectionFromRightToLeft());

                page.Background().Element(bg => Background(bg, draft));

                page.Header().ContentFromRightToLeft().Element(c => Header(c, d, qr, G, year, draft));
                page.Content().ContentFromRightToLeft().Element(c => Body(c, d, G, members, sections, dissentSections, rebuttalSections));
            });
        }).GeneratePdf();
    }

    // ── Header (repeats on every printed page) ──
    private static void Header(IContainer c, CopyRequestDetail d, byte[] qr, Func<string, string> G, string year, bool draft)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(96).Column(q =>
                {
                    if (!draft)
                    {
                        q.Item().Width(84).Image(qr);
                    }
                    
                    q.Item().AlignCenter().Text(d.CopyNumber ?? (d.MiscNumber is { } mm ? $"متفرق {mm}" : "—")).FontSize(8);
                });
                row.RelativeItem().AlignMiddle().Column(t =>
                {
                    t.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(d.CourtName) ? "محكمة النقض" : d.CourtName).Bold().FontSize(17);
                    t.Item().AlignCenter().Text("إعلام الحكم").Bold().FontSize(11);
                    // BR-11: a متفرق is based on an original copy — show the link.
                    if (!string.IsNullOrWhiteSpace(d.OriginalCopyNumber))
                        t.Item().AlignCenter().Text($"قرار متفرق — مستند إلى النسخة: {d.OriginalCopyNumber}").FontSize(9);
                });
                row.ConstantItem(96); // balances the QR so the title stays centred
            });

            // Meta bar: رقم القرار dead-centre, لعام at the side, no case base number.
            col.Item().PaddingTop(8).BorderTop(1.6f).BorderBottom(1.6f).BorderColor(Colors.Black)
                .PaddingVertical(6).Row(m =>
                {
                    // رقم المتفرق shown on the start side for متفرق copies (otherwise empty balancer).
                    m.RelativeItem().Text(d.MiscNumber is { } misc ? $"رقم المتفرق: {misc}" : "").Bold();
                    m.RelativeItem().AlignCenter().Text($"رقم القرار: {Dash(G("decisionNumber"))}").Bold();
                    m.RelativeItem().AlignLeft().Text($"لعام: {Dash(year)}").Bold();
                });
        });
    }

    // ── Body ──
    private static void Body(IContainer c, CopyRequestDetail d, Func<string, string> G,
        IReadOnlyList<(string Name, string Title, bool Dissenting, bool Replying)> members,
        IReadOnlyList<(string Title, string Text)> sections,
        IReadOnlyList<(string Title, string Text)> dissentSections,
        IReadOnlyList<(string Title, string Text)> rebuttalSections)
    {
        c.PaddingTop(12).Column(col =>
        {
            col.Spacing(10);

            col.Item().AlignCenter().Text(t =>
            {
                t.Line("بسم الله الرّحمن الرّحيم").Bold();
                t.Line("باسم الشعب العربي في سورية").Bold();
            });

            // Judging panel.
            col.Item().Text(t =>
            {
                t.Span("الهيئة الحاكمة: ").Bold();
                t.Span(Dash(G("chamber")));
                t.Span(" — المؤلفة من السادة القضاة:");
            });
            col.Item().PaddingHorizontal(20).Column(p =>
            {
                // Titles (صفات) are chosen by the copyist; fall back to the classic roles for
                // copies saved before per-member titles existed.
                var presidentTitle = G("presidentTitle");
                p.Item().Element(e => PanelRow(e, Dash(G("president")), string.IsNullOrWhiteSpace(presidentTitle) ? "رئيساً" : presidentTitle));
                foreach (var m in members)
                    p.Item().Element(e => PanelRow(e, m.Name, string.IsNullOrWhiteSpace(m.Title) ? "مستشاراً" : m.Title));
            });

            // Dissenting judges (رأي مخالف): the president and/or any member flagged as dissenting.
            // Computed once — used both for the note on this page (before the signatures) and the
            // dissent appendix on the following page.
            var dissenters = new List<(string Name, string Title)>();
            if (string.Equals(G("presidentDissenting"), "true", StringComparison.OrdinalIgnoreCase))
                dissenters.Add((G("president"), G("presidentTitle")));
            foreach (var m in members)
                if (m.Dissenting) dissenters.Add((m.Name, m.Title));

            // Inserted sections, in order. Section text may carry inline bold/italic (rendered as
            // styled runs); the title is plain.
            foreach (var s in sections)
                col.Item().Column(sec =>
                {
                    if (!string.IsNullOrWhiteSpace(s.Title)) sec.Item().Text($"{s.Title}:").Bold();
                    sec.Item().PaddingTop(4).Text(t => RenderRich(t, s.Text, 1.9f));
                });

            // Issue date line.
            col.Item().PaddingTop(20).AlignCenter()
                .Text($"قراراً صدر في {Dots(G("issueHijri"))} هـ الموافق لـ {Dots(G("issueGregorian"))} م").Bold();

            // A dissent exists → flag it on the decision page itself, BEFORE the signatures, naming
            // the dissenting judges. The full reasoning is in the الرأي المخالف appendix that follows.
            if (dissenters.Count > 0)
                col.Item().PaddingTop(10).Text(t =>
                {
                    t.Span("صدر هذا القرار مع وجود رأي مخالف. القضاة المخالفون: ").Bold();
                    t.Span(string.Join("، ", dissenters.Select(x => Dash(x.Name))));
                    t.Span(" (انظر ملحق الرأي المخالف).");
                });

            // Signatures — each over the title the copyist chose.
            col.Item().PaddingTop(40).Row(r =>
            {
                foreach (var m in members)
                    r.RelativeItem().Element(e => Signature(e, string.IsNullOrWhiteSpace(m.Title) ? "المستشار" : m.Title, m.Name));
                var presidentTitle = G("presidentTitle");
                r.RelativeItem().Element(e => Signature(e, string.IsNullOrWhiteSpace(presidentTitle) ? "الرئيس" : presidentTitle, G("president")));
            });

            // Footer line (no case base number).
            col.Item().PaddingTop(18).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(6).Row(f =>
            {
                f.RelativeItem().Text($"نسخ: {d.AssignedCopyistName ?? "—"}").FontSize(9).FontColor(Colors.Grey.Darken2);
                f.RelativeItem().AlignLeft().Text(d.CopyNumber is { } cn ? $"رقم النسخة: {cn}" : $"رقم المتفرق: {d.MiscNumber}").FontSize(9).FontColor(Colors.Grey.Darken2);
            });

            // ── Dissent appendix (الرأي المخالف) — printed on a NEW page whenever one or more judges
            // dissent (dissenters computed above). Holds the reason (same paragraph style) and is
            // signed by the dissenting judges ONLY; the reason text comes from DissentSectionsJson.
            if (dissenters.Count > 0)
            {
                col.Item().PageBreak();
                col.Item().PaddingBottom(6).AlignCenter().Text("الرأي المخالف").Bold().FontSize(15);
                foreach (var s in dissentSections)
                    col.Item().Column(sec =>
                    {
                        if (!string.IsNullOrWhiteSpace(s.Title)) sec.Item().Text($"{s.Title}:").Bold();
                        sec.Item().PaddingTop(4).Text(t => RenderRich(t, s.Text, 1.9f));
                    });
                // Signatures of the dissenting judges (each over the title chosen for them).
                col.Item().PaddingTop(40).Row(r =>
                {
                    foreach (var dj in dissenters)
                        r.RelativeItem().Element(e => Signature(e, string.IsNullOrWhiteSpace(dj.Title) ? "القاضي المخالف" : dj.Title, dj.Name));
                });
            }

            // ── Reply-to-dissent appendix (الرد على الرأي المخالف) — the majority's response to the
            // dissent, printed on a NEW page AFTER the dissent appendix. Only when a dissent exists
            // AND one or more judges are flagged as replying (presidentReplying + members[].Replying).
            // A replying judge is never also a dissenting judge (enforced on the save side). Signed by
            // the replying judges ONLY; the reason text comes from RebuttalSectionsJson.
            var repliers = new List<(string Name, string Title)>();
            if (string.Equals(G("presidentReplying"), "true", StringComparison.OrdinalIgnoreCase))
                repliers.Add((G("president"), G("presidentTitle")));
            foreach (var m in members)
                if (m.Replying) repliers.Add((m.Name, m.Title));

            if (dissenters.Count > 0 && repliers.Count > 0)
            {
                col.Item().PageBreak();
                col.Item().PaddingBottom(6).AlignCenter().Text("الرد على الرأي المخالف").Bold().FontSize(15);
                foreach (var s in rebuttalSections)
                    col.Item().Column(sec =>
                    {
                        if (!string.IsNullOrWhiteSpace(s.Title)) sec.Item().Text($"{s.Title}:").Bold();
                        sec.Item().PaddingTop(4).Text(t => RenderRich(t, s.Text, 1.9f));
                    });
                col.Item().PaddingTop(40).Row(r =>
                {
                    foreach (var rj in repliers)
                        r.RelativeItem().Element(e => Signature(e, string.IsNullOrWhiteSpace(rj.Title) ? "القاضي" : rj.Title, rj.Name));
                });
            }
        });
    }

    private static void PanelRow(IContainer c, string name, string role) =>
        c.PaddingVertical(2).Row(r =>
        {
            r.RelativeItem().Text(string.IsNullOrWhiteSpace(name) ? "……………" : name);
            r.ConstantItem(90).AlignLeft().Text(role);
        });

    private static void Signature(IContainer c, string role, string name) =>
        c.AlignCenter().Column(col =>
        {
            col.Item().AlignCenter().Text(role).Bold();
            col.Item().PaddingTop(30).AlignCenter().Text(name);
        });

    // Background watermarks: a faint centred court logo on every page, plus (for non-approved
    // copies) the repeated "مسودة قرار" stamp drawn over it.
    private static void Background(IContainer c, bool draft) =>
        c.Layers(layers =>
        {
            layers.PrimaryLayer().AlignCenter().AlignMiddle();
            if (!draft) 
                layers.Layer().AlignCenter().AlignMiddle().Width(115, Unit.Millimetre).Image(LogoFaint);

            if (draft) 
                layers.Layer().Element(DraftWatermark);
        });

    private static void DraftWatermark(IContainer c) =>
        c.AlignMiddle().Column(col =>
        {
            col.Spacing(46);
            col.Item().OffsetX(100).OffsetY(50).Rotate(-30).Text("غير مثبت").FontFamily(Font).FontSize(100).Bold().FontColor("#b7aeae");
        });

    // ── Parsing helpers ──
    private static Dictionary<string, string> ParseFields(string json)
    {
        var map = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "") : p.Value.GetRawText();
        }
        catch { /* malformed → empty; never throw on rendering */ }
        return map;
    }

    // Panel members: new shape is an array of { judge, title } objects; legacy copies stored a
    // plain array of judge-name strings (rendered with the default "مستشاراً" title downstream).
    private static List<(string Name, string Title, bool Dissenting, bool Replying)> ParsePanelMembers(string json)
    {
        var list = new List<(string, string, bool, bool)>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String)
                    {
                        var s = e.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) list.Add((s!.Trim(), "", false, false));
                    }
                    else if (e.ValueKind == JsonValueKind.Object)
                    {
                        var name = Str(e, "judge");
                        if (name.Length == 0) name = Str(e, "name");
                        var title = Str(e, "title");
                        var dissenting = e.TryGetProperty("dissenting", out var dv) && dv.ValueKind == JsonValueKind.True;
                        var replying = e.TryGetProperty("replying", out var rv) && rv.ValueKind == JsonValueKind.True;
                        if (name.Length > 0) list.Add((name, title, dissenting, replying));
                    }
                }
        }
        catch { /* ignore */ }
        return list;

        static string Str(JsonElement e, string prop) =>
            e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
    }

    // Renders the constrained rich-text subset (<b>, <i>, <br> + escaped text — see RichText on the
    // save side) into QuestPDF styled runs so bold/italic survive into the printed PDF.
    private static void RenderRich(TextDescriptor t, string html, float lineHeight)
    {
        bool bold = false, italic = false;
        var buf = new System.Text.StringBuilder(html?.Length ?? 0);

        void Flush()
        {
            if (buf.Length == 0) return;
            var span = t.Span(buf.ToString()).LineHeight(lineHeight);
            if (bold) span.Bold();
            if (italic) span.Italic();
            buf.Clear();
        }

        if (string.IsNullOrEmpty(html)) { t.Span(""); return; }

        int i = 0, n = html.Length;
        while (i < n)
        {
            var c = html[i];
            if (c == '<')
            {
                var gt = html.IndexOf('>', i + 1);
                if (gt < 0) { buf.Append(c); i++; continue; }
                var tag = html.Substring(i + 1, gt - i - 1).Trim().ToLowerInvariant();
                i = gt + 1;
                switch (tag)
                {
                    case "b" or "strong": Flush(); bold = true; break;
                    case "/b" or "/strong": Flush(); bold = false; break;
                    case "i" or "em": Flush(); italic = true; break;
                    case "/i" or "/em": Flush(); italic = false; break;
                    case "br" or "br/" or "br /": buf.Append('\n'); break;
                }
            }
            else if (c == '&')
            {
                var semi = html.IndexOf(';', i + 1);
                if (semi > i && semi - i <= 10) { buf.Append(DecodeEntity(html.Substring(i, semi - i + 1))); i = semi + 1; }
                else { buf.Append('&'); i++; }
            }
            else { buf.Append(c); i++; }
        }
        Flush();
    }

    private static string DecodeEntity(string e) => e switch
    {
        "&lt;" => "<",
        "&gt;" => ">",
        "&amp;" => "&",
        "&quot;" => "\"",
        "&#39;" or "&apos;" => "'",
        "&nbsp;" => " ",
        _ => e,
    };

    private static List<(string Title, string Text)> ParseSections(string json)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var text = e.TryGetProperty("text", out var x) ? x.GetString() ?? "" : "";
                    list.Add((title, text));
                }
        }
        catch { /* ignore */ }
        return list;
    }

    private static byte[] QrPng(string content)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    private static string Dash(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
    private static string Dots(string s) => string.IsNullOrWhiteSpace(s) ? "……" : s;

    private static Stream Resource(Assembly asm, string name) =>
        asm.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Embedded resource not found: {name}");

    private static byte[] ReadAll(Assembly asm, string name)
    {
        using var s = Resource(asm, name);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
