using System.Numerics;
using SearchContracts;
using SearchEngine;
using SearchEngine.Index;

namespace SearchServer.Endpoints;

public static class IndexEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost(ApiRoutes.Index, BuildAsync);
        app.MapGet(ApiRoutes.Stats, (EngineState state) => state.Current is { } i
            ? new StatsResponse(i.Docs.Length, i.BuildTimeMs, i.ThreadsUsed, i.Terms.TermCount, i.Terms.PostingCount, ModeName(i.Terms.Mode))
            : new StatsResponse(0, 0, 0, 0, 0, ModeName(LockMode.Striped)));
    }

    private static async Task<IResult> BuildAsync(
        IndexRequest? request, IndexService service, ServerOptions options, CancellationToken ct)
    {
        var threads = request?.Threads ?? options.SearchThreads;
        var segments = request?.Segments ?? Defaults.IndexSegments;
        if (threads is < ApiRoutes.MinThreads or > ApiRoutes.MaxThreads)
        {
            return Error(StatusCodes.Status400BadRequest, $"threads must be between {ApiRoutes.MinThreads} and {ApiRoutes.MaxThreads}");
        }

        if (!TryParseMode(request?.LockMode, out var mode))
        {
            return Error(StatusCodes.Status400BadRequest, $"lock_mode must be '{ApiRoutes.LockModeStriped}' or '{ApiRoutes.LockModeGlobal}'");
        }

        if (segments < 1 || !BitOperations.IsPow2(segments))
        {
            return Error(StatusCodes.Status400BadRequest, "segments must be a positive power of two");
        }

        try
        {
            var index = await service.BuildAsync(threads, mode, segments, ct);
            return Results.Ok(new IndexResponse(ApiRoutes.HealthStatusOk, index.Docs.Length, index.BuildTimeMs, index.ThreadsUsed, ModeName(mode)));
        }
        catch (BuildInProgressException e)
        {
            return Error(StatusCodes.Status409Conflict, e.Message);
        }
        catch (DirectoryNotFoundException)
        {
            return Error(StatusCodes.Status400BadRequest, $"data directory not found: {options.DataDir}");
        }
    }

    internal static IResult Error(int status, string message) => Results.Json(new ErrorResponse(message), ApiRoutes.Json, statusCode: status);

    private static bool TryParseMode(string? text, out LockMode mode)
    {
        mode = LockMode.Striped;
        switch (text)
        {
            case null or ApiRoutes.LockModeStriped:
                return true;
            case ApiRoutes.LockModeGlobal:
                mode = LockMode.Global;
                return true;
            default:
                return false;
        }
    }

    private static string ModeName(LockMode mode) => mode == LockMode.Global ? ApiRoutes.LockModeGlobal : ApiRoutes.LockModeStriped;
}
