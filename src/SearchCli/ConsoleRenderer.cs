using System.Globalization;
using System.Text.RegularExpressions;
using SearchContracts;

namespace SearchCli;

public class ConsoleRenderer(TextWriter output, bool useColor)
{
    private const string SnippetIndent = "   ";

    public void Expansion(IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            output.WriteLine(Style(CliDefaults.AnsiDim, "Expanded: (none - expansion unavailable or no related terms)"));
            return;
        }

        output.WriteLine($"Expanded: {string.Join(", ", terms)}");
    }

    public void Results(IReadOnlyList<SearchResultDto> results, long timeMs, string query)
    {
        var highlight = HighlightPattern(query);
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            output.WriteLine($"{i + 1}. {result.Title}  ({result.Score.ToString(CliDefaults.ScoreFormat, CultureInfo.InvariantCulture)})");
            output.WriteLine(SnippetIndent + Highlight(result.Snippet, highlight));
        }

        output.WriteLine($"{results.Count} results in {timeMs} ms");
    }

    private Regex? HighlightPattern(string query)
    {
        var terms = Regex.Matches(query, @"[\p{L}\p{N}]+").Select(m => Regex.Escape(m.Value)).ToList();
        return useColor && terms.Count > 0
            ? new Regex($@"\b(?:{string.Join("|", terms)})\b", RegexOptions.IgnoreCase)
            : null;
    }

    private static string Highlight(string snippet, Regex? pattern) =>
        pattern is null
            ? snippet
            : pattern.Replace(snippet, m => CliDefaults.AnsiBoldYellow + m.Value + CliDefaults.AnsiReset);

    private string Style(string code, string text) => useColor ? code + text + CliDefaults.AnsiReset : text;
}
