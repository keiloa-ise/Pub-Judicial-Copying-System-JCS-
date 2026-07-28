using ResourceIQ.Jcs.Application.Common;
using Xunit;

namespace ResourceIQ.Jcs.Tests;

public class CorrectionMetricsTests
{
    [Fact]
    public void Identical_text_has_no_corrections()
    {
        var d = CorrectionMetrics.Diff("قرار المحكمة نهائي", "قرار المحكمة نهائي");
        Assert.Equal(0, d.Added);
        Assert.Equal(0, d.Removed);
        Assert.Equal(0, d.Corrected);
    }

    [Fact]
    public void Substituting_a_word_counts_one_add_and_one_delete()
    {
        var d = CorrectionMetrics.Diff("a b c", "a x c");
        Assert.Equal(1, d.Added);
        Assert.Equal(1, d.Removed);
        Assert.Equal(2, d.Corrected); // substitution = delete + insert
    }

    [Fact]
    public void Pure_insertions_and_deletions_are_counted()
    {
        Assert.Equal(new CorrectionMetrics.WordDelta(2, 0), CorrectionMetrics.Diff("a b", "a b c d"));
        Assert.Equal(new CorrectionMetrics.WordDelta(0, 2), CorrectionMetrics.Diff("a b c d", "a b"));
    }

    [Fact]
    public void Empty_baseline_counts_everything_as_added()
    {
        Assert.Equal(new CorrectionMetrics.WordDelta(3, 0), CorrectionMetrics.Diff("", "one two three"));
        Assert.Equal(new CorrectionMetrics.WordDelta(0, 3), CorrectionMetrics.Diff("one two three", null));
    }

    [Fact]
    public void ExtractPlainText_flattens_sections_and_strips_markup()
    {
        const string json = "[{\"title\":\"الديباجة\",\"text\":\"قرار <b>المحكمة</b> نهائي\"}]";
        var text = CorrectionMetrics.ExtractPlainText(json);
        Assert.Contains("المحكمة", text);
        Assert.DoesNotContain("<b>", text);
        Assert.Equal(4, CorrectionMetrics.WordCount(text)); // الديباجة + قرار + المحكمة + نهائي
    }

    [Fact]
    public void WordCount_splits_on_whitespace()
    {
        Assert.Equal(0, CorrectionMetrics.WordCount("   "));
        Assert.Equal(3, CorrectionMetrics.WordCount(" one\ttwo\nthree "));
    }
}
