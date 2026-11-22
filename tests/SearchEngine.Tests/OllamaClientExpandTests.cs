using System.Net;
using System.Text.Json;
using SearchEngine.Llm;

namespace SearchEngine.Tests;

public class OllamaClientExpandTests
{
    private const string Model = "gemma4:e2b";
    private const int ShortTimeoutMs = 50;
    private static readonly OllamaOptions Options = new("http://ollama:11434", Model, 1000);

    private static HttpResponseMessage Generated(string response) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { response })) };

    private static Task<ExpansionResult> Expand(string query, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, OllamaOptions? options = null) =>
        new OllamaClient(new HttpClient(new StubHandler(respond)), options ?? Options).ExpandAsync(query, CancellationToken.None);

    private static Task<ExpansionResult> Expand(string query, string response) =>
        Expand(query, (_, _) => Task.FromResult(Generated(response)));

    [Fact]
    public async Task Sends_model_prompt_json_format()
    {
        string? body = null;
        await Expand("ocean", async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Generated("""{"terms":[]}""");
        });

        using var json = JsonDocument.Parse(body!);
        var root = json.RootElement;
        Assert.Equal(Model, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        var prompt = root.GetProperty("prompt").GetString();
        Assert.Contains("ocean", prompt);
        Assert.Contains(Defaults.MaxExpandedTerms.ToString(), prompt);
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
    }

    [Fact]
    public async Task Parses_terms()
    {
        var result = await Expand("ocean", """{"terms":["sea","marine"]}""");

        Assert.Null(result.Error);
        Assert.Equal(["sea", "marine"], result.Terms);
    }

    [Fact]
    public async Task Sanitizes_terms()
    {
        var extra = string.Join(",", Enumerable.Range(0, 30).Select(i => $"\"word{i}\""));
        var result = await Expand("ocean", $$"""{"terms":["Ocean","deep sea","sea","marine life",{{extra}}]}""");

        Assert.DoesNotContain("ocean", result.Terms);
        Assert.Equal(["deep", "sea", "marine", "life"], result.Terms.Take(4));
        Assert.Equal(result.Terms.Distinct().Count(), result.Terms.Count);
        Assert.Equal(8, result.Terms.Count);
    }

    [Fact]
    public async Task Bad_json_returns_empty_with_error()
    {
        var result = await Expand("ocean", "not json");

        Assert.Empty(result.Terms);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"terms":null}""")]
    [InlineData("""{"words":["sea"]}""")]
    public async Task Wrong_shape_model_reply_returns_empty_with_error(string response)
    {
        var result = await Expand("ocean", response);

        Assert.Empty(result.Terms);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("""{"response":null}""")]
    [InlineData("{}")]
    public async Task Wrong_shape_envelope_returns_empty_with_error(string envelope)
    {
        var result = await Expand("ocean", (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) }));

        Assert.Empty(result.Terms);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Http_failure_returns_empty_with_error()
    {
        var result = await Expand("ocean", (_, _) => throw new HttpRequestException());

        Assert.Empty(result.Terms);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Timeout_returns_empty_with_error()
    {
        var result = await Expand("ocean", async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return Generated("""{"terms":["sea"]}""");
        }, Options with { TimeoutMs = ShortTimeoutMs });

        Assert.Empty(result.Terms);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var client = new OllamaClient(new HttpClient(new StubHandler((_, ct) =>
            Task.FromCanceled<HttpResponseMessage>(ct))), Options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ExpandAsync("ocean", cts.Token));
    }
}
