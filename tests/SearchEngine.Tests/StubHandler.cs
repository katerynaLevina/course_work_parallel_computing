namespace SearchEngine.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public StubHandler(Func<HttpResponseMessage> respond) : this((_, _) => Task.FromResult(respond()))
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        respond(request, ct);
}
