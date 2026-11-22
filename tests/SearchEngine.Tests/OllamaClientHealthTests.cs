using System.Net;
using SearchEngine.Llm;

namespace SearchEngine.Tests;

public class OllamaClientHealthTests
{
    private const string Model = "gemma4:e2b";
    private static readonly OllamaOptions Options = new("http://ollama:11434", Model, 1000);

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond());
    }

    private static Task<bool> IsAvailable(Func<HttpResponseMessage> respond) =>
        new OllamaClient(new HttpClient(new StubHandler(respond)), Options).IsAvailableAsync(CancellationToken.None);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Available_when_tags_lists_model() =>
        Assert.True(await IsAvailable(() => Json("""{"models":[{"name":"gemma4:e2b"}]}""")));

    [Fact]
    public async Task Available_when_tags_lists_model_with_latest_suffix() =>
        Assert.True(await new OllamaClient(new HttpClient(new StubHandler(() => Json("""{"models":[{"name":"llama3:latest"}]}"""))),
            Options with { Model = "llama3" }).IsAvailableAsync(CancellationToken.None));

    [Fact]
    public async Task Unavailable_when_model_missing() =>
        Assert.False(await IsAvailable(() => Json("""{"models":[{"name":"llama3"}]}""")));

    [Fact]
    public async Task Unavailable_when_http_fails() =>
        Assert.False(await IsAvailable(() => throw new HttpRequestException()));
}
