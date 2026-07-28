using System.Text;
using System.Text.Json;

namespace ResourceIQ.Jcs.Application.Common;

/// <summary>
/// Word-level correction measurement for the copyist-accuracy report. Given the decision content
/// at the moment it was returned and at the moment it was re-submitted, it counts how many WORDS the
/// copyist corrected (additions + deletions from a word-level LCS diff — a substituted word counts as
/// one deletion + one insertion). "Correctness of writing" is the primary performance signal, so this
/// magnitude matters far more than the number of return cycles.
/// </summary>
public static class CorrectionMetrics
{
    /// <summary>Words added and removed between two content snapshots.</summary>
    public readonly record struct WordDelta(int Added, int Removed)
    {
        /// <summary>Total corrected words = additions + deletions.</summary>
        public int Corrected => Added + Removed;
    }

    /// <summary>Flattens a SectionsJson array (<c>[{title,text}, …]</c>) to plain text: each section's
    /// title (already plain) plus its text stripped of the inline b/i/br markup, space-joined. On any
    /// parse error the raw input is treated as plain text (defensive — mirrors RichText).</summary>
    public static string ExtractPlainText(string? sectionsJson)
    {
        if (string.IsNullOrWhiteSpace(sectionsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(sectionsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return RichText.StripToPlainText(sectionsJson);

            var sb = new StringBuilder();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var title = el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                var text = el.TryGetProperty("text", out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
                if (!string.IsNullOrEmpty(title)) { sb.Append(RichText.StripToPlainText(title)); sb.Append(' '); }
                if (!string.IsNullOrEmpty(text)) { sb.Append(RichText.StripToPlainText(text)); sb.Append(' '); }
            }
            return sb.ToString();
        }
        catch (JsonException)
        {
            return RichText.StripToPlainText(sectionsJson);
        }
    }

    /// <summary>Splits text into words on any Unicode whitespace; punctuation stays attached (a good
    /// enough token for measuring correction magnitude in Arabic legal prose).</summary>
    public static string[] Tokenize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static int WordCount(string? text) => Tokenize(text).Length;

    /// <summary>Word-level diff between two plain-text snapshots. Uses an O(n·m)-time / O(min)-space
    /// longest-common-subsequence: additions = new words not on the common subsequence, deletions =
    /// old words not on it. Order-sensitive, so moved or reworded passages register as corrections.</summary>
    public static WordDelta Diff(string? oldText, string? newText)
    {
        var a = Tokenize(oldText);
        var b = Tokenize(newText);
        if (a.Length == 0) return new WordDelta(b.Length, 0);
        if (b.Length == 0) return new WordDelta(0, a.Length);

        // Keep the shorter sequence on the inner axis so the row buffers are min(len)+1.
        var lcs = LcsLength(a, b);
        return new WordDelta(Added: b.Length - lcs, Removed: a.Length - lcs);
    }

    private static int LcsLength(string[] a, string[] b)
    {
        // Ensure b is the shorter axis for the O(min) row buffers.
        if (b.Length > a.Length) (a, b) = (b, a);
        int m = b.Length;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        foreach (var ai in a)
        {
            for (int j = 1; j <= m; j++)
                curr[j] = string.Equals(ai, b[j - 1], StringComparison.Ordinal)
                    ? prev[j - 1] + 1
                    : Math.Max(prev[j], curr[j - 1]);
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}
