using System.Text.RegularExpressions;

namespace SearchEngine.Search;

public static partial class SnippetBuilder
{
    public static string Build(string text, IReadOnlyCollection<string> terms, int radius = Defaults.SnippetRadius)
    {
        var start = 0;
        var end = Math.Min(text.Length, 2 * radius);
        if (terms.Count > 0)
        {
            var pattern = $@"\b(?:{string.Join('|', terms.Select(Regex.Escape))})\b";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                (start, end) = Window(text, match.Index, match.Index + match.Length, radius);
            }
        }

        var snippet = Whitespace().Replace(text[start..end], " ").Trim();
        return (start > 0 ? Defaults.Ellipsis : "") + snippet + (end < text.Length ? Defaults.Ellipsis : "");
    }

    /// <summary>The match widened by the radius on both sides, shrunk back to whole words.</summary>
    private static (int Start, int End) Window(string text, int matchStart, int matchEnd, int radius)
    {
        var start = Math.Max(0, matchStart - radius);
        while (start > 0 && start < matchStart && !char.IsWhiteSpace(text[start - 1]))
        {
            start++;
        }

        var end = Math.Min(text.Length, matchEnd + radius);
        while (end < text.Length && end > matchEnd && !char.IsWhiteSpace(text[end]))
        {
            end--;
        }

        return (start, end);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
