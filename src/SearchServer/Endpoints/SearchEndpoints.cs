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

        if (SearchRequestRules.ValidateQuery(request?.Query) is { } queryError)
        {
            return IndexEndpoints.Error(StatusCodes.Status400BadRequest, queryError);
        }

        var query = request!.Query!;
        var topK = SearchRequestRules.ClampTopK(request.TopK);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var expanded = SearchRequestRules.ShouldExpand(request.Expand) ? (await service.ExpandAsync(query, ct)).Terms : [];

            var result = await service.KeywordSearchAsync(query, expanded, topK);
            var results = result.Hits.Select(SearchRequestRules.ToDto).ToList();
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

        try
        {
            return Results.Ok(new DocResponse(doc.DocId, doc.Title, await File.ReadAllTextAsync(doc.Path, ct)));
        }
        catch (IOException)
        {
            // Indexed, but the file was removed from disk since.
            return IndexEndpoints.Error(StatusCodes.Status404NotFound, ApiRoutes.ErrorDocNotFound);
        }
    }
}
