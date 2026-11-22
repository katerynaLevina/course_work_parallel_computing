using System.Diagnostics;
using SearchContracts;
using SearchEngine;

namespace SearchServer.Endpoints;

public static class SearchEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost(ApiRoutes.Search, SearchAsync);
        app.MapGet(ApiRoutes.Doc, GetDocAsync);
    }

    private static async Task<IResult> SearchAsync(SearchRequest? request, SearchService service, CancellationToken ct)
    {
        if (request?.Mode is not (null or ApiRoutes.SearchModeKeyword))
        {
            return IndexEndpoints.Error(StatusCodes.Status400BadRequest, ApiRoutes.ErrorUnknownMode);
        }

        if (request?.Query is not { } query)
        {
            return IndexEndpoints.Error(StatusCodes.Status400BadRequest, ApiRoutes.ErrorQueryRequired);
        }

        if (query.Length > ApiRoutes.MaxQueryLength)
        {
            return IndexEndpoints.Error(StatusCodes.Status400BadRequest, ApiRoutes.ErrorQueryTooLong);
        }

        var topK = Math.Clamp(request.TopK ?? Defaults.DefaultTopK, ApiRoutes.MinTopK, ApiRoutes.MaxTopK);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var expanded = (request.Expand ?? ApiRoutes.DefaultExpand) ? (await service.ExpandAsync(query, ct)).Terms : [];

            var result = await service.KeywordSearchAsync(query, expanded, topK);
            var results = result.Hits.Select(h => new SearchResultDto(h.DocId, h.Score, h.Snippet, h.Title)).ToList();
            return Results.Ok(new SearchResponse(results, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, expanded));
        }
        catch (IndexNotBuiltException e)
        {
            return IndexEndpoints.Error(StatusCodes.Status409Conflict, e.Message);
        }
    }

    private static async Task<IResult> GetDocAsync(string docId, EngineState state, CancellationToken ct)
    {
        if (state.Current?.FindDoc(docId) is not { } doc)
        {
            return IndexEndpoints.Error(StatusCodes.Status404NotFound, ApiRoutes.ErrorDocNotFound);
        }

        return Results.Ok(new DocResponse(doc.DocId, doc.Title, await File.ReadAllTextAsync(doc.Path, ct)));
    }
}
