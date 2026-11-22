using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using SearchContracts;

namespace SearchCli;

public sealed class ApiClient(string server) : IDisposable
{
    private readonly HttpClient http = new() { BaseAddress = new Uri(server), Timeout = CliDefaults.HttpTimeout };

    public Task<SearchResponse> SearchAsync(string query, int topK, bool expand, CancellationToken ct) =>
        PostAsync<SearchResponse>(ApiRoutes.Search, new SearchRequest(query, ApiRoutes.SearchModeKeyword, expand, topK), ct);

    public Task<IndexResponse> IndexAsync(int? threads, CancellationToken ct) =>
        PostAsync<IndexResponse>(ApiRoutes.Index, new IndexRequest(threads, null, null), ct);

    public Task<StatsResponse> StatsAsync(CancellationToken ct) => GetAsync<StatsResponse>(ApiRoutes.Stats, ct);

    public Task<HealthResponse> HealthAsync(CancellationToken ct) => GetAsync<HealthResponse>(ApiRoutes.Health, ct);

    /// <summary>Streams a search over the WebSocket, rendering each stage as soon as it arrives.</summary>
    public async Task StreamSearchAsync(string query, int topK, ConsoleRenderer renderer, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        var wsUri = new UriBuilder(http.BaseAddress!) { Scheme = http.BaseAddress!.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs, Path = ApiRoutes.WsSearch };
        await socket.ConnectAsync(wsUri.Uri, ct);

        var request = JsonSerializer.SerializeToUtf8Bytes(new WsRequest(query, topK), ApiRoutes.Json);
        await socket.SendAsync(request, WebSocketMessageType.Text, true, ct);

        while (true)
        {
            var message = await ReceiveAsync(socket, ct) ?? throw new HttpRequestException("server closed the connection early");
            var data = message.Data is JsonElement element ? element : default;
            switch (message.Stage)
            {
                case ApiRoutes.StageQueryExpansion:
                    renderer.Expansion(data.Deserialize<ExpansionStageData>(ApiRoutes.Json)!.ExpandedTerms);
                    break;
                case ApiRoutes.StageKeywordSearch:
                    var keyword = data.Deserialize<KeywordStageData>(ApiRoutes.Json)!;
                    renderer.Results(keyword.Results, keyword.TimeMs, query);
                    break;
                case ApiRoutes.StageError:
                    throw new HttpRequestException(data.Deserialize<ErrorStageData>(ApiRoutes.Json)!.Message);
                case ApiRoutes.StageDone:
                    return;
            }
        }
    }

    public void Dispose() => http.Dispose();

    private static async Task<StageMessage?> ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[ApiRoutes.WsReceiveBufferBytes];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonSerializer.Deserialize<StageMessage>(message.ToArray(), ApiRoutes.Json);
    }

    private async Task<T> GetAsync<T>(string route, CancellationToken ct) =>
        await ReadAsync<T>(await http.GetAsync(route, ct), ct);

    private async Task<T> PostAsync<T>(string route, object body, CancellationToken ct) =>
        await ReadAsync<T>(await http.PostAsJsonAsync(route, body, ApiRoutes.Json, ct), ct);

    /// <summary>The server's error text, or a generic status message when the body is not an error JSON object.</summary>
    public static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(ApiRoutes.Json, ct);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return error.Error;
            }
        }
        catch (JsonException)
        {
        }

        return string.Format(CliDefaults.MessageStatus, (int)response.StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await ErrorMessageAsync(response, ct));
            }

            return (await response.Content.ReadFromJsonAsync<T>(ApiRoutes.Json, ct))!;
        }
    }
}
