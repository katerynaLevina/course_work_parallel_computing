using SearchCli;

namespace SearchCli.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Positional_query_means_search()
    {
        var options = CliOptions.Parse(["ocean waves"]);

        Assert.Equal(CliDefaults.CommandSearch, options.Command);
        Assert.Equal("ocean waves", options.Query);
        Assert.True(options.Expand);
        Assert.Equal(CliDefaults.DefaultTopK, options.TopK);
    }

    [Fact]
    public void Parses_flags()
    {
        var options = CliOptions.Parse(["--rest", "--no-expand", "--top", "5", "q"]);

        Assert.True(options.Rest);
        Assert.False(options.Expand);
        Assert.Equal(5, options.TopK);
    }

    [Fact]
    public void Index_with_threads()
    {
        var options = CliOptions.Parse(["--index", "8"]);

        Assert.Equal(CliDefaults.CommandIndex, options.Command);
        Assert.Equal(8, options.Threads);
    }

    [Fact]
    public void No_args_is_usage_error()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse([]));
    }
}
