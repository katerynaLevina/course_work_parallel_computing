using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SearchContracts;
using SearchEngine.Index;

namespace SearchServer.Tests;

public class IndexEndpointTests
{
    private static Task<HttpResponseMessage> PostIndex(HttpClient c, string body) =>
        c.PostAsync(ApiRoutes.Index, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Index_builds_and_reports_documents()
    {
        using var f = new ServerFactory();
        var r = await PostIndex(f.CreateClient(), "{\"threads\":2}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", json);
        Assert.Contains($"\"documents_indexed\":{ServerFactory.CorpusSize}", json);
        Assert.Contains("\"time_ms\":", json);
    }

    [Fact]
    public async Task Stats_reflect_last_build()
    {
        using var f = new ServerFactory();
        var c = f.CreateClient();
        await PostIndex(c, "{\"threads\":2}");
        var json = await c.GetStringAsync(ApiRoutes.Stats);
        Assert.Contains($"\"documents\":{ServerFactory.CorpusSize}", json);
        Assert.Contains("\"threads_used\":2", json);
    }

    [Fact]
    public async Task Stats_before_build_are_zero()
    {
        using var f = new ServerFactory();
        var s = await f.CreateClient().GetFromJsonAsync<StatsResponse>(ApiRoutes.Stats, ApiRoutes.Json);
        Assert.Equal(0, s!.Documents);
        Assert.Equal(0, s.IndexBuildTimeMs);
        Assert.Equal(0, s.ThreadsUsed);
    }

    [Theory]
    [InlineData("{\"threads\":0}")]
    [InlineData("{\"threads\":1000}")]
    [InlineData("{\"threads\":2,\"lock_mode\":\"nope\"}")]
    [InlineData("{\"threads\":2,\"segments\":3}")]
    public async Task Invalid_index_request_is_400(string body)
    {
        using var f = new ServerFactory();
        var r = await PostIndex(f.CreateClient(), body);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("\"error\":", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_build_returns_409()
    {
        using var f = new ServerFactory();
        var state = f.Services.GetRequiredService<EngineState>();
        var service = new IndexService(state, f.Services.GetRequiredService<ServerOptions>());

        Assert.True(state.TryBeginBuild());
        await Assert.ThrowsAsync<BuildInProgressException>(() => service.BuildAsync(2, LockMode.Striped, 16, default));

        var r = await PostIndex(f.CreateClient(), "{\"threads\":2}");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);

        state.EndBuild();
        Assert.Equal(HttpStatusCode.OK, (await PostIndex(f.CreateClient(), "{\"threads\":2}")).StatusCode);
    }

    [Fact]
    public async Task Old_index_is_released_when_a_rebuild_starts()
    {
        using var f = new ServerFactory();
        var state = f.Services.GetRequiredService<EngineState>();
        var service = new IndexService(state, f.Services.GetRequiredService<ServerOptions>());
        await service.BuildAsync(2, LockMode.Striped, 16, default);
        Assert.NotNull(state.Current);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.BuildAsync(2, LockMode.Striped, 16, cts.Token));

        Assert.Null(state.Current);
    }

    [Fact]
    public async Task Missing_data_dir_keeps_the_old_index()
    {
        using var f = new ServerFactory();
        var state = f.Services.GetRequiredService<EngineState>();
        var options = f.Services.GetRequiredService<ServerOptions>();
        await new IndexService(state, options).BuildAsync(2, LockMode.Striped, 16, default);
        var old = state.Current;

        var missing = options with { DataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) };
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new IndexService(state, missing).BuildAsync(2, LockMode.Striped, 16, default));

        Assert.Same(old, state.Current);
    }

    [Fact]
    public async Task Missing_data_dir_is_400()
    {
        using var f = new ServerFactory { DataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) };
        var r = await PostIndex(f.CreateClient(), "{\"threads\":2}");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.StartsWith("data directory not found: ", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Global_lock_mode_is_reported()
    {
        using var f = new ServerFactory();
        var c = f.CreateClient();
        await PostIndex(c, "{\"threads\":2,\"lock_mode\":\"global\"}");
        Assert.Contains("\"lock_mode\":\"global\"", await c.GetStringAsync(ApiRoutes.Stats));
    }
}
