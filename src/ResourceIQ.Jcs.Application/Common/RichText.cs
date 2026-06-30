using System.Text;
using System.Text.Json;

namespace ResourceIQ.Jcs.Application.Common;

/// <summary>
/// Section text may carry inline BOLD/ITALIC formatting the copyist applies while editing
/// (reflected verbatim in the printed PDF). To keep legally-significant content safe, formatting
/// is stored as a tightly-constrained HTML subset — only <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c> and
/// <c>&lt;br&gt;</c> — and ALL other markup is stripped on save (client input is never trusted).
///
/// Plain text is never dropped or altered: it is HTML-escaped, so what the user typed is preserved
/// exactly (e.g. a literal '&lt;' becomes '&amp;lt;' and renders back as '&lt;'). The matching
/// reader on the print side decodes this subset into styled runs.
/// </summary>
public static class RichText
{
    /// <summary>
    /// Sanitizes the section texts inside a SectionsJson array: each section's <c>text</c> is
    /// reduced to the safe formatting subset, and its <c>title</c> to plain text. Returns the
    /// re-serialized array. On any parse error the input is returned unchanged (the caller's
    /// validation/`UpdateContent` still guards state; rendering is defensive too).
    /// </summary>
    public static string SanitizeSectionsJson(string? sectionsJson)
    {
        if (string.IsNullOrWhiteSpace(sectionsJson)) return "[]";
        try
        {
            using var doc = JsonDocument.Parse(sectionsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return sectionsJson;

            var sections = new List<Dictionary<string, string>>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var title = el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                var text = el.TryGetProperty("text", out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "";
                sections.Add(new Dictionary<string, string>
                {
                    ["title"] = StripToPlainText(title),
                    ["text"] = SanitizeFormatted(text),
                });
            }
            // Do not escape non-ASCII (Arabic) — keep the JSON human-readable and compact.
            return JsonSerializer.Serialize(sections, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
        catch (JsonException)
        {
            return sectionsJson;
        }
    }

    /// <summary>Reduce arbitrary input to the safe formatting subset: escaped text + balanced
    /// &lt;b&gt;/&lt;i&gt; runs + &lt;br&gt; line breaks. Unknown tags are dropped; block tags
    /// (div/p) become line breaks.</summary>
    public static string SanitizeFormatted(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        int bOpen = 0, iOpen = 0;
        int i = 0, n = input.Length;
        while (i < n)
        {
            var c = input[i];
            if (c == '<')
            {
                var gt = input.IndexOf('>', i + 1);
                if (gt < 0) { AppendEscaped(sb, c); i++; continue; } // stray '<' → text
                var name = TagName(input, i + 1, gt, out var closing);
                i = gt + 1;
                switch (name)
                {
                    case "b" or "strong":
                        if (closing) { if (bOpen > 0) { sb.Append("</b>"); bOpen--; } }
                        else { sb.Append("<b>"); bOpen++; }
                        break;
                    case "i" or "em":
                        if (closing) { if (iOpen > 0) { sb.Append("</i>"); iOpen--; } }
                        else { sb.Append("<i>"); iOpen++; }
                        break;
                    case "br":
                        sb.Append("<br>");
                        break;
                    case "div" or "p":
                        if (!closing && sb.Length > 0) sb.Append("<br>"); // a new block ⇒ line break
                        break;
                    // anything else (span, script, style, …) is dropped entirely
                }
            }
            else if (c == '&')
            {
                var semi = input.IndexOf(';', i + 1);
                if (semi > i && semi - i <= 10 && IsEntity(input, i + 1, semi))
                { sb.Append(input, i, semi - i + 1); i = semi + 1; }
                else { sb.Append("&amp;"); i++; }
            }
            else { AppendEscaped(sb, c); i++; }
        }
        while (bOpen-- > 0) sb.Append("</b>");
        while (iOpen-- > 0) sb.Append("</i>");
        return sb.ToString();
    }

    /// <summary>Removes all markup and decodes entities — used for plain fields (section titles).</summary>
    public static string StripToPlainText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        int i = 0, n = input.Length;
        while (i < n)
        {
            var c = input[i];
            if (c == '<')
            {
                var gt = input.IndexOf('>', i + 1);
                if (gt < 0) { sb.Append(c); i++; continue; }
                i = gt + 1; // drop the tag
            }
            else if (c == '&')
            {
                var semi = input.IndexOf(';', i + 1);
                if (semi > i && semi - i <= 10 && IsEntity(input, i + 1, semi))
                { sb.Append(DecodeEntity(input.Substring(i, semi - i + 1))); i = semi + 1; }
                else { sb.Append(c); i++; }
            }
            else { sb.Append(c); i++; }
        }
        return sb.ToString().Trim();
    }

    private static string TagName(string s, int start, int end, out bool closing)
    {
        closing = start < end && s[start] == '/';
        if (closing) start++;
        var j = start;
        while (j < end && (char.IsLetter(s[j]) || char.IsDigit(s[j]))) j++;
        return s[start..j].ToLowerInvariant();
    }

    private static bool IsEntity(string s, int start, int semi)
    {
        if (start >= semi) return false;
        if (s[start] == '#')
        {
            var k = start + 1;
            var hex = k < semi && (s[k] == 'x' || s[k] == 'X');
            if (hex) k++;
            if (k >= semi) return false;
            for (; k < semi; k++)
                if (!(hex ? Uri.IsHexDigit(s[k]) : char.IsDigit(s[k]))) return false;
            return true;
        }
        for (var k = start; k < semi; k++)
            if (!char.IsLetter(s[k])) return false;
        return true;
    }

    private static string DecodeEntity(string e) => e switch
    {
        "&lt;" => "<",
        "&gt;" => ">",
        "&amp;" => "&",
        "&quot;" => "\"",
        "&#39;" or "&apos;" => "'",
        "&nbsp;" => " ",
        _ => DecodeNumeric(e),
    };

    private static string DecodeNumeric(string e)
    {
        if (e.Length > 3 && e[1] == '#')
        {
            var body = e[2..^1];
            var hex = body.Length > 0 && (body[0] == 'x' || body[0] == 'X');
            var num = hex ? body[1..] : body;
            if (int.TryParse(num, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var code) && code is > 0 and <= 0x10FFFF)
                return char.ConvertFromUtf32(code);
        }
        return e; // unknown → leave verbatim
    }

    private static void AppendEscaped(StringBuilder sb, char c)
    {
        switch (c)
        {
            case '<': sb.Append("&lt;"); break;
            case '>': sb.Append("&gt;"); break;
            case '&': sb.Append("&amp;"); break;
            default: sb.Append(c); break;
        }
    }
}
