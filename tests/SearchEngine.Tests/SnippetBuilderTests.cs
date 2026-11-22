using SearchEngine.Search;

namespace SearchEngine.Tests;

public class SnippetBuilderTests
{
    private const int Radius = 20;

    [Fact]
    public void Window_contains_match_with_ellipses()
    {
        var text = string.Join(' ', Enumerable.Repeat("filler", 30)) + " OCEAN " + string.Join(' ', Enumerable.Repeat("padding", 30));

        var snippet = SnippetBuilder.Build(text, ["ocean"], Radius);

        Assert.StartsWith(Defaults.Ellipsis, snippet);
        Assert.EndsWith(Defaults.Ellipsis, snippet);
        Assert.Contains("OCEAN", snippet);
        Assert.True(snippet.Length <= 2 * Radius + 7 + 2 * Defaults.Ellipsis.Length);
        Assert.DoesNotContain("fillerfiller", snippet);
    }

    [Fact]
    public void Matches_whole_words_only()
    {
        var snippet = SnippetBuilder.Build("oceanic things and the ocean", ["ocean"], Radius);

        Assert.Contains("the ocean", snippet);
    }

    [Fact]
    public void No_match_returns_text_start()
    {
        var text = new string('a', 10) + " " + new string('b', 100);

        var snippet = SnippetBuilder.Build(text, ["zebra"], Radius);

        Assert.StartsWith("aaaaaaaaaa", snippet);
        Assert.EndsWith(Defaults.Ellipsis, snippet);
    }

    [Fact]
    public void Short_text_has_no_ellipsis()
    {
        Assert.Equal("a ocean b", SnippetBuilder.Build("a ocean b", ["ocean"], Radius));
    }

    [Fact]
    public void Collapses_whitespace()
    {
        Assert.Equal("a ocean b c", SnippetBuilder.Build("a\n\n  ocean\t\tb \r\n c", ["ocean"], Radius));
    }
}
