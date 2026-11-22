using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SearchContracts;

namespace SearchServer.Endpoints;

public static class WebSocketEndpoints
{
    public static void Map(WebApplication app) => app.Map(ApiRoutes.WsSearch, HandleAsync);

    private static async Task HandleAsync(HttpContext context, SearchService service)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var ct = context.RequestAborted;
        try
        {
            while (await ReceiveTextAsync(socket, ct) is { } text)
            {
                await RunPipelineAsync(socket, service, text, ct);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            // The client went away; nothing left to do.
        }
    }

    /// <summary>Assembles one text message from its fragments; null when the client closed. An oversized message yields an empty string.</summary>
    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[ApiRoutes.WsReceiveBufferBytes];
        using var message = new MemoryStream();
        var tooLarge = false;
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                return null;
            }

            tooLarge |= message.Length + result.Count > ApiRoutes.MaxWsMessageBytes;
            if (!tooLarge)
            {
                message.Write(buffer, 0, result.Count);
            }
        }
        while (!result.EndOfMessage);

        return tooLarge ? string.Empty : Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    private static async Task RunPipelineAsync(WebSocket socket, SearchService service, string text, CancellationToken ct)
    {
        if (text.Length == 0)
        {
            await SendErrorAsync(socket, ApiRoutes.ErrorMessageTooLarge, ct);
            return;
        }

        WsRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<WsRequest>(text, ApiRoutes.Json);
        }
        catch (JsonException)
        {
            await SendErrorAsync(socket, ApiRoutes.ErrorInvalidJson, ct);
            return;
        }

        if (request?.Query is not { } query)
        {
            await SendErrorAsync(socket, ApiRoutes.ErrorQueryRequired, ct);
            return;
        }

        if (query.Length > ApiRoutes.MaxQueryLength)
        {
            await SendErrorAsync(socket, ApiRoutes.ErrorQueryTooLong, ct);
            return;
        }

        try
        {
            var expansion = await service.ExpandAsync(query, ct);
            await SendAsync(socket, new StageMessage(ApiRoutes.StageQueryExpansion, new ExpansionStageData(query, expansion.Terms, expansion.Error)), ct);

            var keyword = await service.KeywordSearchAsync(query, expansion.Terms, SearchEndpoints.ClampTopK(request.TopK));
            var results = keyword.Hits.Select(SearchEndpoints.ToDto).ToList();
            await SendAsync(socket, new StageMessage(ApiRoutes.StageKeywordSearch, new KeywordStageData(results, keyword.TimeMs)), ct);

            await SendAsync(socket, new StageMessage(ApiRoutes.StageDone, null), ct);
        }
        catch (IndexNotBuiltException e)
        {
            await SendErrorAsync(socket, e.Message, ct);
        }
    }

    private static Task SendErrorAsync(WebSocket socket, string message, CancellationToken ct) =>
        SendAsync(socket, new StageMessage(ApiRoutes.StageError, new ErrorStageData(message)), ct);

    private static Task SendAsync(WebSocket socket, StageMessage message, CancellationToken ct) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, ApiRoutes.Json), WebSocketMessageType.Text, true, ct);
}
