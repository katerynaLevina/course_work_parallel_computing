using SearchContracts;

namespace SearchCli.Tests;

public class ConsoleRendererTests
{
    private static readonly SearchResultDto[] Results =
    [
        new("ocean.txt", 0.95, "waves on the ocean", "Ocean Facts"),
    ];

    [Fact]
    public void Renders_results_without_color()
    {
        var output = new StringWriter();
        var renderer = new ConsoleRenderer(output, useColor: false);

        renderer.Expansion(["sea", "marine"]);
        renderer.Results(Results, 56, "ocean");

        var text = output.ToString();
        Assert.Contains("Expanded: sea, marine", text);
        Assert.Contains("1. Ocean Facts", text);
        Assert.Contains("(0.9500)", text);
        Assert.Contains("results in", text);
        Assert.DoesNotContain(CliDefaults.AnsiBoldYellow, text);
    }

    [Fact]
    public void Highlights_query_terms_with_color()
    {
        var output = new StringWriter();
        var renderer = new ConsoleRenderer(output, useColor: true);

        renderer.Results(Results, 1, "ocean");

        Assert.Contains(CliDefaults.AnsiBoldYellow + "ocean" + CliDefaults.AnsiReset, output.ToString());
    }
}
