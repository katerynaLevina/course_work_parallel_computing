using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SearchContracts;

namespace SearchServer.Tests;

public class SearchEndpointTests
{
    private static Task<HttpResponseMessage> Post(HttpClient c, string route, string body) =>
        c.PostAsync(route, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

    private static async Task<HttpClient> IndexedClient(ServerFactory f)
    {
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(c, ApiRoutes.Index, "{\"threads\":2}")).StatusCode);
        return c;
    }

    private static async Task<SearchResponse> Search(HttpClient c, string body)
    {
        var r = await Post(c, ApiRoutes.Search, body);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SearchResponse>(ApiRoutes.Json))!;
    }

    [Fact]
    public async Task Search_returns_contract_shape()
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        var r = await Post(c, ApiRoutes.Search, "{\"query\":\"term3\",\"mode\":\"keyword\"}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var first = doc.RootElement.GetProperty("results")[0];
        Assert.Equal("doc03", first.GetProperty("doc_id").GetString());
        Assert.True(first.GetProperty("score").GetDouble() > 0);
        Assert.False(string.IsNullOrEmpty(first.GetProperty("snippet").GetString()));
        Assert.True(doc.RootElement.TryGetProperty("time_ms", out _));
    }

    [Fact]
    public async Task Search_uses_expanded_terms()
    {
        using var f = new ServerFactory();
        f.Expander.Terms = ["term7"];
        var c = await IndexedClient(f);
        var r = await Search(c, "{\"query\":\"term3\"}");
        Assert.Contains(r.Results, x => x.DocId == "doc07");
        Assert.Equal(["term7"], r.ExpandedTerms);
    }

    [Fact]
    public async Task Expand_false_skips_expander()
    {
        using var f = new ServerFactory();
        f.Expander.Terms = ["term7"];
        var c = await IndexedClient(f);
        var r = await Search(c, "{\"query\":\"term3\",\"expand\":false}");
        Assert.Equal(0, f.Expander.Calls);
        Assert.Empty(r.ExpandedTerms);
        Assert.DoesNotContain(r.Results, x => x.DocId == "doc07");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!")]
    [InlineData("the of")]
    public async Task Contentless_query_returns_empty_results(string q)
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        var r = await Search(c, JsonSerializer.Serialize(new { query = q }));
        Assert.Empty(r.Results);
    }

    [Fact]
    public async Task Unknown_mode_is_400()
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(c, ApiRoutes.Search, "{\"query\":\"alpha\",\"mode\":\"semantic\"}")).StatusCode);
    }

    [Fact]
    public async Task Missing_query_is_400()
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(c, ApiRoutes.Search, "{}")).StatusCode);
    }

    [Fact]
    public async Task Search_before_index_is_409()
    {
        using var f = new ServerFactory();
        var r = await Post(f.CreateClient(), ApiRoutes.Search, "{\"query\":\"alpha\"}");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains($"\"error\":\"{ApiRoutes.ErrorIndexNotBuilt}\"", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Too_long_query_is_400()
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        var body = JsonSerializer.Serialize(new { query = new string('a', ApiRoutes.MaxQueryLength + 1) });
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(c, ApiRoutes.Search, body)).StatusCode);
    }

    [Fact]
    public async Task Doc_endpoint_returns_text_and_404()
    {
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        var doc = await c.GetFromJsonAsync<DocResponse>(ApiRoutes.DocPrefix + "doc05", ApiRoutes.Json);
        Assert.Equal("doc05", doc!.DocId);
        Assert.Contains("term5", doc.Text);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(ApiRoutes.DocPrefix + "missing")).StatusCode);
    }

    [Fact]
    public async Task Search_before_index_does_not_call_expander()
    {
        using var f = new ServerFactory();
        var r = await Post(f.CreateClient(), ApiRoutes.Search, "{\"query\":\"alpha\"}");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(0, f.Expander.Calls);
    }

    [Fact]
    public async Task Searches_during_rebuild_are_200_or_409()
    {
        const int MinSearchesDuringBuilds = 50;
        const int MaxBuilds = 50;
        using var f = new ServerFactory();
        var c = await IndexedClient(f);
        var state = f.Services.GetRequiredService<EngineState>();
        var completed = 0;
        using var stop = new CancellationTokenSource();
        var firstDone = new CountdownEvent(20);

        var searchers = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            var statuses = new List<HttpStatusCode>();
            do
            {
                statuses.Add((await Post(c, ApiRoutes.Search, "{\"query\":\"alpha\"}")).StatusCode);
                Interlocked.Increment(ref completed);
                if (statuses.Count == 1)
                {
                    firstDone.Signal();
                }
            }
            while (!stop.IsCancellationRequested);
            return statuses;
        })).ToList();

        Assert.True(firstDone.Wait(TimeSpan.FromSeconds(30)));
        var duringBuilds = 0;
        for (var i = 0; i < MaxBuilds && duringBuilds < MinSearchesDuringBuilds; i++)
        {
            var start = Volatile.Read(ref completed);
            Assert.Equal(HttpStatusCode.OK, (await Post(c, ApiRoutes.Index, "{\"threads\":2}")).StatusCode);
            duringBuilds += Volatile.Read(ref completed) - start;
        }

        await stop.CancelAsync();
        var all = (await Task.WhenAll(searchers).WaitAsync(TimeSpan.FromSeconds(30))).SelectMany(s => s).ToList();
        Assert.True(duringBuilds >= MinSearchesDuringBuilds, $"only {duringBuilds} searches overlapped builds");
        Assert.All(all, s => Assert.True(s is HttpStatusCode.OK or HttpStatusCode.Conflict, $"unexpected {s}"));
        Assert.NotNull(state.Current);
        Assert.Equal(HttpStatusCode.OK, (await Post(c, ApiRoutes.Search, "{\"query\":\"alpha\"}")).StatusCode);
    }
}
