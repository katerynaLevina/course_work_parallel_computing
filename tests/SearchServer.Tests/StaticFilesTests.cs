using System.Net;

namespace SearchServer.Tests;

public class StaticFilesTests
{
    [Fact]
    public async Task Root_serves_index_html()
    {
        using var f = new ServerFactory();
        var client = f.CreateClient();

        var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains("id=\"search-input\"", await page.Content.ReadAsStringAsync());

        var script = await client.GetAsync("/app.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
    }
}
