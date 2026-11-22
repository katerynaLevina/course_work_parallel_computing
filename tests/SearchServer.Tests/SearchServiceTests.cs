using Microsoft.Extensions.DependencyInjection;
using SearchEngine.Concurrency;

namespace SearchServer.Tests;

public class SearchServiceTests
{
    [Fact]
    public async Task Keyword_search_without_index_throws()
    {
        using var pool = new WorkerPool(1);
        var service = new SearchService(new EngineState(), new FakeExpander(), pool);
        await Assert.ThrowsAsync<IndexNotBuiltException>(() => service.KeywordSearchAsync("alpha", [], 5));
    }

    [Fact]
    public async Task Expand_delegates_to_the_expander()
    {
        using var f = new ServerFactory();
        f.Expander.Delay = TimeSpan.FromMilliseconds(50);
        using var client = f.CreateClient();
        await client.PostAsync(SearchContracts.ApiRoutes.Index, new StringContent("{\"threads\":2}", System.Text.Encoding.UTF8, "application/json"));
        var service = f.Services.GetRequiredService<SearchService>();
        var result = await service.ExpandAsync("alpha", default);
        Assert.Empty(result.Terms);
        Assert.Equal(1, f.Expander.Calls);
    }

    [Fact]
    public async Task Expand_without_index_throws_before_calling_expander()
    {
        using var f = new ServerFactory();
        var service = f.Services.GetRequiredService<SearchService>();
        await Assert.ThrowsAsync<IndexNotBuiltException>(() => service.ExpandAsync("alpha", default));
        Assert.Equal(0, f.Expander.Calls);
    }

    [Fact]
    public async Task Keyword_search_runs_bm25_and_reports_time()
    {
        using var f = new ServerFactory();
        using var client = f.CreateClient();
        await client.PostAsync(SearchContracts.ApiRoutes.Index, new StringContent("{\"threads\":2}", System.Text.Encoding.UTF8, "application/json"));
        var service = f.Services.GetRequiredService<SearchService>();
        var result = await service.KeywordSearchAsync("term4", [], 5);
        Assert.Equal("doc04", result.Hits[0].DocId);
        Assert.True(result.TimeMs >= 0);
    }
}
