using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SearchContracts;

namespace SearchServer.Tests;

public class WebSocketTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static async Task Index(ServerFactory f)
    {
        var r = await f.CreateClient().PostAsync(ApiRoutes.Index, new StringContent("{\"threads\":2}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static async Task<WebSocket> Connect(ServerFactory f, CancellationToken ct) =>
        await f.Server.CreateWebSocketClient().ConnectAsync(new Uri(f.Server.BaseAddress, ApiRoutes.WsSearch), ct);

    private static Task Send(WebSocket ws, string text, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    private static async Task<JsonElement> Receive(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var result = await ws.ReceiveAsync(buffer, ct);
        return JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count)).RootElement;
    }

    private static async Task<List<JsonElement>> Query(WebSocket ws, string query, CancellationToken ct)
    {
        await Send(ws, $"{{\"query\":\"{query}\"}}", ct);
        var messages = new List<JsonElement>();
        do
        {
            messages.Add(await Receive(ws, ct));
        }
        while (messages[^1].GetProperty("stage").GetString() is not (ApiRoutes.StageDone or ApiRoutes.StageError));

        return messages;
    }

    private static string[] Stages(List<JsonElement> messages) => messages.Select(m => m.GetProperty("stage").GetString()!).ToArray();

    [Fact]
    public async Task Streams_three_stages_in_order()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        using var ws = await Connect(f, cts.Token);
        var messages = await Query(ws, "ocean", cts.Token);
        Assert.Equal([ApiRoutes.StageQueryExpansion, ApiRoutes.StageKeywordSearch, ApiRoutes.StageDone], Stages(messages));
        Assert.Equal("ocean", messages[0].GetProperty("data").GetProperty("original").GetString());
        Assert.True(messages[1].GetProperty("data").TryGetProperty("time_ms", out _));
        Assert.False(messages[2].TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Two_queries_on_one_socket()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        using var ws = await Connect(f, cts.Token);
        var first = await Query(ws, "term3", cts.Token);
        var second = await Query(ws, "term5", cts.Token);
        Assert.Equal("doc03", first[1].GetProperty("data").GetProperty("results")[0].GetProperty("doc_id").GetString());
        Assert.Equal("doc05", second[1].GetProperty("data").GetProperty("results")[0].GetProperty("doc_id").GetString());
    }

    [Fact]
    public async Task Invalid_json_yields_error_stage()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        using var ws = await Connect(f, cts.Token);
        await Send(ws, "not json", cts.Token);
        var error = await Receive(ws, cts.Token);
        Assert.Equal(ApiRoutes.StageError, error.GetProperty("stage").GetString());
        Assert.Equal(ApiRoutes.ErrorInvalidJson, error.GetProperty("data").GetProperty("message").GetString());
        Assert.Equal(ApiRoutes.StageDone, (await Query(ws, "term3", cts.Token))[^1].GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Missing_query_yields_error_stage()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        using var ws = await Connect(f, cts.Token);
        await Send(ws, "{}", cts.Token);
        Assert.Equal(ApiRoutes.ErrorQueryRequired, (await Receive(ws, cts.Token)).GetProperty("data").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Oversized_message_yields_error_stage_and_socket_stays_open()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        using var ws = await Connect(f, cts.Token);
        await Send(ws, new string('x', ApiRoutes.MaxWsMessageBytes + 1), cts.Token);
        Assert.Equal(ApiRoutes.ErrorMessageTooLarge, (await Receive(ws, cts.Token)).GetProperty("data").GetProperty("message").GetString());
        Assert.Equal(ApiRoutes.StageDone, (await Query(ws, "term3", cts.Token))[^1].GetProperty("stage").GetString());
    }

    [Fact]
    public async Task No_index_yields_error_stage()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        using var ws = await Connect(f, cts.Token);
        var messages = await Query(ws, "ocean", cts.Token);
        Assert.Equal([ApiRoutes.StageError], Stages(messages));
        Assert.Equal(ApiRoutes.ErrorIndexNotBuilt, messages[0].GetProperty("data").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Plain_http_request_is_rejected_with_400()
    {
        using var f = new ServerFactory();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.CreateClient().GetAsync(ApiRoutes.WsSearch)).StatusCode);
    }

    [Fact]
    public async Task Client_abort_mid_pipeline_does_not_break_server()
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var f = new ServerFactory();
        await Index(f);
        f.Expander.Delay = TimeSpan.FromMilliseconds(500);
        var ws = await Connect(f, cts.Token);
        await Send(ws, "{\"query\":\"term3\"}", cts.Token);
        ws.Abort();
        ws.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(800), cts.Token);

        Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync(ApiRoutes.Health, cts.Token)).StatusCode);
        f.Expander.Delay = TimeSpan.Zero;
        using var again = await Connect(f, cts.Token);
        Assert.Equal(ApiRoutes.StageDone, (await Query(again, "term3", cts.Token))[^1].GetProperty("stage").GetString());
    }
}
