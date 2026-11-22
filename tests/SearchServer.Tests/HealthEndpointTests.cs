using System.Net;
using SearchContracts;

namespace SearchServer.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_returns_ok_and_ollama_false_when_unavailable()
    {
        using var f = new ServerFactory();
        f.FakeOllama.Available = false;
        var r = await f.CreateClient().GetAsync(ApiRoutes.Health);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", json);
        Assert.Contains("\"ollama\":false", json);
    }

    [Fact]
    public async Task Health_reports_ollama_true_when_available()
    {
        using var f = new ServerFactory();
        f.FakeOllama.Available = true;
        var r = await f.CreateClient().GetAsync(ApiRoutes.Health);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("\"ollama\":true", await r.Content.ReadAsStringAsync());
    }
}
